using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Discord;
using Hartsy.Core.Commands;
using Hartsy.Core.SupaBase.Models;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Hartsy.Core.SupaBase;
using Microsoft.IdentityModel.Tokens;
using Hartsy.Core.ImageUtil;

namespace Hartsy.Core.InteractionComponents
{
    public class SelectMenus(UserCommands commands, SupabaseClient supaBase, StableSwarmAPI stableSwarmAPI) : InteractionModuleBase<SocketInteractionContext>
    {

        /// <summary>Handles the interaction when an image is selected for a specific action, such as showcasing or saving to gallery.
        /// Verifies the user's identity, checks if the image file exists, validates the user's subscription and credit, 
        /// and executes the appropriate action based on the selected option.</summary>
        /// <param name="customId">The custom ID associated with the select menu that triggered the interaction.</param>
        /// <param name="selections">The selections made by the user in the select menu.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("select_image:*")]
        public async Task HandleImageSelect(string customId, string[] selections)
        {
            await DeferAsync();
            string? selectedValue = selections.FirstOrDefault();
            if (!string.IsNullOrEmpty(selectedValue))
            {
                string[] parts = customId.Split(':');
                if (parts.Length < 3) return;
                string actionType = parts[0];
                string userId = parts[1];
                string messageId = parts[2];
                if (Context.User.Id.ToString() != userId)
                {
                    await RespondWithError("Selection Error", "Error: You cannot select another user's image.");
                    return;
                }
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), $"../../../images/{Context.User.Username}/{messageId}/{messageId}-{selectedValue}");
                if (!File.Exists(filePath))
                {
                    await FollowupAsync("Error: Image not found.", ephemeral: true);
                    return;
                }
                SocketGuildUser? user = Context.User as SocketGuildUser;
                Users? supaUser = await supaBase.GetUserByDiscordId(user!.Id.ToString());
                if (supaUser == null || supaUser.PlanName == null || supaUser.Credit <= 0)
                {
                    await UserCommands.HandleSubscriptionFailure(Context);
                    return;
                }
                int credits = supaUser.Credit ?? 0;
                await FollowupAsync(embed: CreateCreditEmbed(credits), ephemeral: true);
                switch (actionType)
                {
                    case "i2i":
                        await HandleImageToImage(user!, messageId, filePath);
                        break;
                    case "add":
                        await HandleAddImageToGallery(user!, supaUser, filePath);
                        break;
                    case "showcase":
                        await Showcase.ShowcaseImageAsync(Context.Guild, filePath, Context.User);
                        await FollowupAsync("Image added to the showcase!", ephemeral: true);
                        break;
                    case "gif":
                        await GenerateGif(filePath);
                        break;
                    default:
                        await FollowupAsync("Invalid action type.", ephemeral: true);
                        break;
                }
            }
        }

        /// <summary>Processes an image-to-image generation request by extracting parameters from the message embed, 
        /// converting the selected image to Base64, and calling the template generation command. 
        /// Updates the user's credit after generation.</summary>
        /// <param name="user">The user who initiated the image-to-image generation.</param>
        /// <param name="messageId">The message ID associated with the original image message.</param>
        /// <param name="filePath">The file path of the selected image.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleImageToImage(SocketGuildUser user, string messageId, string filePath)
        {
            IUserMessage? message = await Context.Channel.GetMessageAsync(Convert.ToUInt64(messageId)) as IUserMessage;
            IEmbed embed = message!.Embeds.First();
            var (text, description, template) = ComponentHelpers.ParseEmbed(embed);
            string initimage = Convert.ToBase64String(File.ReadAllBytes(filePath));
            await commands.GenerateFromTemplate(text, template, Context.Channel as SocketTextChannel, user, description, initimage);
            int newCredit = (await supaBase.GetUserByDiscordId(user.Id.ToString()))!.Credit ?? 0 - 1;
            await supaBase.UpdateUserCredit(user.Id.ToString(), newCredit);
        }

        /// <summary>Manages the addition of an image to the user's gallery. Checks if the user's gallery is full, 
        /// uploads the image to Supabase, adds the image record to the database, 
        /// and sends the image to the user either via DM or channel reply.</summary>
        /// <param name="user">The user who is adding the image to their gallery.</param>
        /// <param name="supaUser">The user's Supabase information.</param>
        /// <param name="filePath">The file path of the image being added.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleAddImageToGallery(SocketGuildUser user, Users supaUser, string filePath)
        {
            int userImageCount = await supaBase.GetUserImageCountInGallery(supaUser.Id!);
            int maxImagesAllowed = SupabaseClient.GetMaxImagesAllowed(supaUser.PlanName);
            if (userImageCount >= maxImagesAllowed)
            {
                await FollowupAsync(embed: CreateGalleryFullEmbed(supaUser, maxImagesAllowed, userImageCount), ephemeral: true);
                return;
            }
            if (supaUser.PlanName == "Free")
            {
                using Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(filePath);
                image.Mutate(x => x.Resize(image.Width / 4, image.Height / 4));
                await ImageGrid.AddWatermarkBottomRight(image);
                image.SaveAsJpeg(filePath);
            }
            string url = await supaBase.UploadImage(supaUser.Id!, filePath);
            if (url != null)
            {
                await supaBase.AddImage(supaUser.Id!, url);
                await SendImageToUserOrFollowup(user, filePath, supaUser.PlanName!, maxImagesAllowed, userImageCount + 1);
            }
            else
            {
                await FollowupAsync("Error saving image.", ephemeral: true);
            }
        }

        /// <summary>Initiates GIF generation from the selected image. Converts the image to Base64, 
        /// creates the payload for GIF generation, and processes the GIF generation updates, 
        /// modifying the progress message with the current status and the generated GIF.</summary>
        /// <param name="filePath">The file path of the image to be converted into a GIF.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task GenerateGif(string filePath)
        {
            // TODO: Remove GPUTs from user's account
            // TODO: On final image, disable the cancel button and add other buttons.
            string initimage = Convert.ToBase64String(File.ReadAllBytes(filePath));
            Dictionary<string, object> payload = await CreateGifPayload(initimage);
            EmbedBuilder startingEmbed = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("GIF Generation")
                .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.Guild.IconUrl)
                .WithDescription("Generating a GIF from the selected image. This may take a few minutes.")
                .WithColor(Discord.Color.Gold)
                .WithCurrentTimestamp();
            string userId = Context.User.Id.ToString();
            string? sessionId = payload.TryGetValue("session_id", out object? sessionIdObj) ? sessionIdObj.ToString() : "";
            ComponentBuilder components = new ComponentBuilder()
            .WithButton("Interrupt", customId: $"interrupt:{userId}:{sessionId}", ButtonStyle.Danger);
            RestUserMessage processingMessage = await Context.Channel.SendMessageAsync(embed: startingEmbed.Build(), components: components.Build());
            await foreach (var (base64String, isFinal, ETR) in stableSwarmAPI.CreateGifAsync(payload))
            {
                await HandleGifGenerationUpdate(processingMessage, base64String, isFinal, ETR);
            }
        }

        /// <summary>Constructs the payload dictionary for GIF generation, including various parameters such as prompt, model, 
        /// image dimensions, video format, and specific settings for GIF creation.</summary>
        /// <param name="initimage">The initial image in Base64 format to be used in the GIF generation.</param>
        /// <returns>A dictionary containing the payload parameters for the GIF generation request.</returns>
        private async Task<Dictionary<string, object>> CreateGifPayload(string initimage)
        {
            string sessionId = await stableSwarmAPI.GetSession();
            int motionBucket = new Random().Next(100, 200);
            return new Dictionary<string, object>
            {
                {"session_id", sessionId},
                {"prompt", "clear vibrant text"},
                {"negativeprompt", "blurry"},
                {"images", 1},
                {"donotsave", true},
                {"model", "StarlightXL.safetensors"},
                {"loras", "an0tha0ne"},
                {"loraweights", 0.8},
                {"width", 1024},
                {"height", 768},
                {"cfgscale", 6.5},
                {"steps", 1},
                {"seed", -1},
                {"sampler", "dpmpp_3m_sde_gpu"},
                {"scheduler", "karras"},
                {"initimage", initimage},
                {"init_image_creativity", 0},
                {"video_model", "OfficialStableDiffusion/svd_xt_1_1.safetensors"},
                {"video_format", "gif"},
                {"videopreviewtype", "animate"},
                {"videoresolution", "image"},
                {"videoboomerang", true},
                {"video_frames", 25},
                {"video_fps", 60},
                {"video_steps", 40},
                {"video_cfg", 2.5},
                {"video_min_cfg", 1},
                {"video_motion_bucket", motionBucket}, // 127 is baseline stay between 100-200 
                {"exactbackendid", 2 },
                //{"internalbackendtype", "swarmswarmbackend"},
            };
        }

        /// <summary>Handles updates during the GIF generation process. Processes the Base64 string of the generated image, 
        /// determines the image format, resizes if necessary, and updates the progress message with the current status and attached GIF image.</summary>
        /// <param name="processingMessage">The message indicating the progress of the GIF generation.</param>
        /// <param name="base64String">The Base64 string of the generated image.</param>
        /// <param name="isFinal">Indicates if the image is the final output.</param>
        /// <param name="ETR">Estimated time remaining for the GIF generation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task HandleGifGenerationUpdate(RestUserMessage processingMessage, string base64String, bool isFinal, string ETR)
        {
            try
            {
                if (base64String.IsNullOrEmpty())
                {
                    EmbedBuilder errorEmbedBuilder = new EmbedBuilder()
                    .WithTitle("No GPUs Available")
                    .WithDescription("**There are no GPUs available.**\n\n" +
                         "If you are seeing this message, it means this is a non peak time and you need to request GIF gens." +
                         "To get it activated, please ping @kalebbroo. Once activated, you will be able to generate GIFs again. " +
                         "We apologize for the inconvenience and appreciate your patience. Keeping the service online when its not " +
                         "being used is just a waste of money.")
                    .WithColor(Discord.Color.Red)
                    .WithFooter("Check back soon or contact support if the issue persists.")
                    .WithCurrentTimestamp();
                    Embed errorEmbed = errorEmbedBuilder.Build();
                    await processingMessage.ModifyAsync(msg =>
                    {
                        msg.Embeds = new Embed[] { errorEmbed };
                    });
                    return;
                }
                byte[] imageData = Convert.FromBase64String(base64String);
                using MemoryStream imageStream = new(imageData);
                MemoryStream embedStream = new();
                imageStream.Position = 0;
                string suffix = GetImageSuffix(imageStream);
                await ProcessImageStream(imageStream, embedStream, suffix, !isFinal);
                FileAttachment file = new(embedStream, $"new_image.{suffix}");
                SocketTextChannel? channel = processingMessage.Channel as SocketTextChannel;
                SocketGuild guild = channel!.Guild;
                string username = processingMessage.Embeds.First().Author!.Value.Name;
                SocketGuildUser? user = guild.Users.FirstOrDefault(u => u.Username == username);
                string userId = user!.Id.ToString();
                Console.WriteLine($"User: {user.Username}"); // debug
                // TODO: Add Template and Text to the embed so the regen button can be used
                EmbedBuilder updatedEmbed = new EmbedBuilder()
                    .WithAuthor(user)
                    .WithTitle(isFinal ? "✨ GIF Generation Completed!" : "✨ GIF Generation in Progress...")
                    .WithThumbnailUrl($"https://github.com/kalebbroo/Hartsy/blob/main/images/logo.png?raw=true")
                    .WithImageUrl($"attachment://new_image.{suffix}")
                    .WithColor(isFinal ? Discord.Color.Green : Discord.Color.Red)
                    .WithDescription($"Estimated Time Remaining: **{ETR}**")
                    .AddField("Status", isFinal ? "✅ Final GIF generated!" : "🔄 Updating GIF...", true)
                    .AddField("Progress", isFinal ? "100%" : "Ongoing", true)
                    .WithFooter(footer => footer
                        .WithText("Powered by Hartsy.AI")
                        .WithIconUrl("https://github.com/kalebbroo/Hartsy/blob/main/images/logo.png?raw=true"))
                    .WithTimestamp(DateTimeOffset.Now);
                ComponentBuilder componentBuilder = new();
                if (isFinal)
                {
                    Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageData);
                    await ImageHelpers.SaveImageAsync(image, username, processingMessage.Id, 5, "gif");
                    componentBuilder
                        .WithButton("Showcase", $"choose_image:showcase:{userId}", ButtonStyle.Primary)
                        .WithButton("Save to Gallery", $"choose_image:save:{userId}", ButtonStyle.Primary)
                        //.WithButton("Regenerate", $"regenerate:{userId}", ButtonStyle.Success) // TODO: Implement regenerate
                        .WithButton("Report", $"report:{userId}", ButtonStyle.Secondary, emote: new Emoji("\u26A0")) // ⚠
                        .WithButton(" ", $"delete:{userId}", ButtonStyle.Danger, emote: new Emoji("\uD83D\uDDD1")); // 🗑
                    await processingMessage.ModifyAsync(msg =>
                    {
                        msg.Embeds = new[] { updatedEmbed.Build() };
                        msg.Content = "";
                        msg.Components = componentBuilder.Build();
                        msg.Attachments = new[] { file };
                    });
                }
                else
                {
                    await processingMessage.ModifyAsync(msg =>
                    {
                        msg.Embeds = new[] { updatedEmbed.Build() };
                        msg.Attachments = new[] { file };
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        /// <summary>Determines the image format suffix (e.g., "gif" or "jpeg") based on the header bytes of the provided image stream. 
        /// Identifies the format by checking specific byte patterns in the image header.</summary>
        /// <param name="imageStream">The memory stream containing the image data.</param>
        /// <returns>A string representing the image format suffix.</returns>
        private static string GetImageSuffix(MemoryStream imageStream)
        {
            byte[] header = new byte[12];
            imageStream.Read(header, 0, header.Length);
            imageStream.Position = 0;
            if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46) return "gif";
            if (header[0] == 0xFF && header[1] == 0xD8) return "jpeg";
            if (header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) return "gif";
            throw new InvalidOperationException("Unsupported image format");
        }

        /// <summary>Processes the image stream for embedding in a message. Resizes preview images, maintains final image sizes, 
        /// and saves the processed image stream in the appropriate format (JPEG or GIF).</summary>
        /// <param name="imageStream">The memory stream containing the image data.</param>
        /// <param name="embedStream">The memory stream to store the processed image data.</param>
        /// <param name="suffix">The image format suffix.</param>
        /// <param name="isPreview">Indicates if the image is a preview and needs resizing.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task ProcessImageStream(MemoryStream imageStream, MemoryStream embedStream, string suffix, bool isPreview)
        {
            try
            {
                if (suffix == "jpeg")
                {
                    using SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(imageStream);
                    if (isPreview)
                    {
                        image.Mutate(i => i.Resize(1024, 768));
                    }
                    await image.SaveAsJpegAsync(embedStream);
                }
                else if (suffix == "gif")
                {
                    using SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(imageStream);
                    if (isPreview)
                    {
                        int newWidth = image.Width * 3;
                        int newHeight = image.Height * 3;
                        image.Mutate(x => x.Resize(newWidth, newHeight));
                    }
                    GifEncoder encoder = new() { ColorTableMode = GifColorTableMode.Global, Quantizer = new WebSafePaletteQuantizer() };
                    await image.SaveAsGifAsync(embedStream, encoder);
                }
                else
                {
                    await imageStream.CopyToAsync(embedStream);
                }
                embedStream.Position = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}\n");
            }
        }

        /// <summary>Creates an embed message indicating the user's current GPUT balance and the expected balance after generating an image. 
        /// Includes information on the generate command and a link to Hartsy.ai for managing subscription and credits.</summary>
        /// <param name="credits">The current GPUT balance of the user.</param>
        /// <returns>An Embed object containing the credit information.</returns>
        private static Embed CreateCreditEmbed(int credits)
        {
            return new EmbedBuilder()
                .WithTitle("Image Generation")
                .WithDescription($"You have {credits} GPUT. You will have {credits - 1} GPUT after this image is generated.")
                .AddField("Generate Command", "This command allows you to generate images based on the text and template you provide. " +
                    "Each generation will use one GPUT from your account.\n\nGo to [Hartsy.ai](https://hartsy.ai) to check sub status or add GPUTs")
                .WithColor(Discord.Color.Gold)
                .WithCurrentTimestamp()
                .Build();
        }

        /// <summary>Generates an embed message indicating that the user's gallery is full. 
        /// Provides details about the user's current plan, the maximum number of allowed images, 
        /// and instructions to upgrade the plan or remove existing images. Includes a link to Hartsy.ai for managing the gallery and subscription.</summary>
        /// <param name="supaUser">The user's Supabase information.</param>
        /// <param name="maxImagesAllowed">The maximum number of images allowed in the user's gallery based on their plan.</param>
        /// <param name="userImageCount">The current number of images in the user's gallery.</param>
        /// <returns>An Embed object indicating that the gallery is full.</returns>
        private static Embed CreateGalleryFullEmbed(Users supaUser, int maxImagesAllowed, int userImageCount)
        {
            return new EmbedBuilder()
                .WithTitle("ERROR: Gallery At Capacity❗")
                .WithDescription($"Your current plan, **{supaUser.PlanName}**, allows for a maximum of {maxImagesAllowed} images. " +
                    $"You currently have {userImageCount} images in your gallery. To add more images, please upgrade your plan or " +
                    $"remove some existing images. Visit [Hartsy.AI](https://hartsy.ai) to manage your plan and gallery.")
                .WithColor(Discord.Color.Red)
                .WithCurrentTimestamp()
                .Build();
        }

        /// <summary>Constructs and sends an error embed message in response to an invalid or unauthorized interaction. 
        /// Takes a title and description as parameters, sets the embed color to red, and includes the current timestamp.</summary>
        /// <param name="title">The title of the error message.</param>
        /// <param name="description">The description of the error message.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RespondWithError(string title, string description)
        {
            EmbedBuilder errorEmbed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(Discord.Color.Red)
                .WithCurrentTimestamp();
            await FollowupAsync(embed: errorEmbed.Build(), ephemeral: true);
        }

        /// <summary>Sends the saved image to the user either via DM or as a follow-up message in the channel. 
        /// Constructs an embed message with the image information and handles the file attachment. 
        /// If DM fails, it falls back to replying in the channel with the embedded image.</summary>
        /// <param name="user">The user to send the image to.</param>
        /// <param name="filePath">The file path of the saved image.</param>
        /// <param name="planName">The name of the user's subscription plan.</param>
        /// <param name="maxImagesAllowed">The maximum number of images allowed in the user's gallery based on their plan.</param>
        /// <param name="userImageCount">The current number of images in the user's gallery.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SendImageToUserOrFollowup(SocketGuildUser user, string filePath, string planName, int maxImagesAllowed, int userImageCount)
        {
            using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read);
            string filename = Path.GetFileName(filePath).Replace(":", "");
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("Image Saved Successfully")
                .WithDescription($"Your image has been added to your gallery. Your **{planName}** plan allows for a maximum of " +
                    $"**{maxImagesAllowed}** images in your gallery. You now have **{userImageCount}** images. Go to [Hartsy.AI](https://hartsy.ai) " +
                    "to manage your subscription, or view and download the uncompressed image. Discord will compress images, so it's best to download a copy from " +
                    "your gallery. However, the image is also available here for your convenience")
                .WithImageUrl($"attachment://{filename}")
                .WithColor(Discord.Color.Green)
                .WithCurrentTimestamp();
            bool isDmEnabled = false;
            try
            {
                fileStream.Position = 0;
                FileAttachment fileAttachment = new(fileStream, filename);
                Console.WriteLine(planName); // debug
                if (planName == "free")
                {
                    // resize image to low quality for free plan
                    using Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(fileStream);
                    image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2));
                    image.SaveAsJpeg(fileStream);
                    fileStream.Position = 0;
                    await user.SendFileAsync(attachment: fileAttachment, embed: embed.Build());
                    isDmEnabled = true;
                    return;
                }
                await user.SendFileAsync(attachment: fileAttachment, embed: embed.Build());
                isDmEnabled = true;
            }
            catch
            {
                Console.WriteLine("DM failed, falling back to channel reply.");
            }
            if (!isDmEnabled)
            {
                fileStream.Position = 0;
                FileAttachment fileAttachment = new(fileStream, filename);
                await FollowupWithFileAsync(attachment: fileAttachment, embed: embed.Build());
            }
        }
    }
}