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
using Hartsy.Core.SupaBase;

namespace Hartsy.Core.InteractionComponents
{
    public class SelectMenus(UserCommands commands, SupabaseClient supaBase, StableSwarmAPI stableSwarmAPI) : InteractionModuleBase<SocketInteractionContext>
    {

        /// <summary>Handles the interaction when an image is selected for a specific action, such as showcasing or saving to gallery.</summary>
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
                if (parts.Length >= 4) return;
                string actionType = parts[0]; // Should give "i2i" or "add"
                string userid = parts[1]; // Should give the userId part
                string messageId = parts[2]; // Should give the messageId part
                SocketMessageComponent? interaction = Context.Interaction as SocketMessageComponent;
                string username = interaction!.User.Username;
                string userId = interaction.User.Id.ToString();
                if (userId != userid)
                {
                    EmbedBuilder errorEmbed = new EmbedBuilder()
                        .WithTitle("Selection Error")
                        .WithDescription("Error: You cannot select another user's image.")
                        .WithColor(Discord.Color.Red)
                        .WithCurrentTimestamp();
                    await FollowupAsync(embed: errorEmbed.Build(), ephemeral: true);
                    return;
                }
                string filePath = "";
                string directoryPath = "";
                string initimage = "";
                try
                {
                    // Construct the full path
                    directoryPath = Path.Combine(Directory.GetCurrentDirectory(), $"../../../images/{username}/{messageId}");
                    filePath = Path.Combine(directoryPath, $"{messageId}:{selectedValue}.jpeg");
                    // Ensure the directory exists
                    if (!Directory.Exists(directoryPath))
                    {
                        Console.WriteLine("Directory does not exist.");
                        // Depending on your requirements, you can create the directory or handle it as an error
                        Directory.CreateDirectory(directoryPath);
                        Console.WriteLine("Directory created.");
                    }
                    // Check if the file exists
                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine("File does not exist.");
                        await FollowupAsync("Error: Image not found.", ephemeral: true);
                        return;  // Exit if the file does not exist
                    }
                    // Proceed with reading the file
                    initimage = Convert.ToBase64String(File.ReadAllBytes(filePath));
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"An I/O error occurred: {ex.Message}");
                    await FollowupAsync("Error processing the image file.", ephemeral: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                    await FollowupAsync("An unexpected error occurred while processing your request.", ephemeral: true);
                }
                SocketTextChannel? channel = Context.Channel as SocketTextChannel;
                SocketGuildUser? user = Context.User as SocketGuildUser;
                Users? supaUser = await supaBase.GetUserByDiscordId(user!.Id.ToString());
                string? subStatus = supaUser!.PlanName;
                if (subStatus == null || supaUser.Credit <= 0)
                {
                    Console.WriteLine($"Subscription status or credit issue. Status: {subStatus}, Credits: {supaUser.Credit}");
                    await UserCommands.HandleSubscriptionFailure(Context);
                    // TODO: Move HandleSubscriptionFailure to a shared helper method

                    return;
                }
                int credits = supaUser.Credit ?? 0;
                Embed creditEmbed = new EmbedBuilder()
                                .WithTitle("Image Generation")
                                .WithDescription($"You have {credits} GPUT. You will have {credits - 1} GPUT after this image is generated.")
                                .AddField("Generate Command", "This command allows you to generate images based on the text and template you provide. " +
                                "Each generation will use one GPUT from your account.\n\nGo to [Hartsy.ai](https://hartsy.ai) to check sub status or add GPUTs")
                                .WithColor(Discord.Color.Gold)
                                .WithCurrentTimestamp()
                                .Build();
                if (File.Exists(filePath))
                {
                    if (actionType == "i2i")
                    {
                        IUserMessage? message = await Context.Channel.GetMessageAsync(Convert.ToUInt64(messageId)) as IUserMessage;
                        IEmbed embed = message!.Embeds.First();
                        var (text, description, template) = ComponentHelpers.ParseEmbed(embed);
                        await FollowupAsync(embed: creditEmbed, ephemeral: true);
                        await commands.GenerateFromTemplate(text, template, channel, user, description, initimage);
                        await supaBase.UpdateUserCredit(user.Id.ToString(), credits - 1);
                    }
                    else if (actionType == "add")
                    {
                        int userImageCount = await supaBase.GetUserImageCountInGallery(supaUser.Id!);
                        int maxImagesAllowed = supaBase.GetMaxImagesAllowed(supaUser.PlanName);
                        if (userImageCount >= maxImagesAllowed)
                        {
                            EmbedBuilder errorEmbed = new EmbedBuilder()
                                .WithTitle("ERROR: Gallery At Capacity❗")
                                .WithDescription($"Your current plan, **{supaUser.PlanName}**, allows for a maximum of {maxImagesAllowed} images. " +
                                    $"You currently have {userImageCount} images in your gallery. To add more images, please upgrade your plan or " +
                                    $"remove some existing images. Visit [Hartsy.AI](https://hartsy.ai) to manage your plan and gallery.")
                                .WithColor(Discord.Color.Red)
                                .WithCurrentTimestamp();
                            await FollowupAsync(embed: errorEmbed.Build(), ephemeral: true);
                            return;
                        }
                        string? supaUserId = supaUser?.Id;
                        string url = await supaBase.UploadImage(supaUserId!, filePath);
                        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read);
                        string filename = Path.GetFileName(filePath).Replace(":", "");
                        Console.WriteLine($"Filename: {filename}"); // Debugging
                        if (url != null)
                        {
                            await supaBase.AddImage(supaUserId!, url);
                            EmbedBuilder embed = new EmbedBuilder()
                                .WithTitle("Image Saved Successfully")
                                .WithDescription($"Your image has been added to your gallery. Your **{supaUser!.PlanName}** plan allows for a maximum of " +
                                $"**{maxImagesAllowed}** images in your gallery. You now have **{userImageCount + 1}** images. Go to [Hartsy.AI](https://hartsy.ai) " +
                                "to manage your subscription, or view and download the uncompressed image. Discord will compress images, so it's best to download a copy from " +
                                "your gallery. However, the image is also available here for your convience")
                                .WithImageUrl($"attachment://{filename}")
                                .WithColor(Discord.Color.Green)
                                .WithCurrentTimestamp();
                            bool isDmEnabled = false;
                            try
                            {
                                fileStream.Position = 0;
                                FileAttachment fileAttachment = new(fileStream, filename);
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
                        else
                        {
                            await FollowupAsync("Error saving image.", ephemeral: true);
                        }
                    }
                    else if (actionType == "showcase")
                    {
                        await Showcase.ShowcaseImageAsync(Context.Guild, filePath, Context.User);
                        await FollowupAsync("Image added to the showcase!", ephemeral: true);
                    }
                    else if (actionType == "gif")
                    {
                        Dictionary<string, object> payload = new()
                        {
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
                            {"initimage", initimage!},
                            {"init_image_creativity", 0},
                            // Video-specific parameters //
                            {"video_model", "OfficialStableDiffusion/svd_xt_1_1.safetensors"},
                            {"video_format", "gif"},
                            {"videopreviewtype", "animate"},
                            {"videoresolution", "image"},
                            {"videoboomerang", true},
                            {"video_frames", 25},
                            {"video_fps", 60},
                            {"video_steps", 22},
                            {"video_cfg", 2.5},
                            {"video_min_cfg", 1},
                            {"video_motion_bucket", 127},
                        };
                        RestUserMessage processingMessage = await Context.Channel.SendMessageAsync("Starting GIF generation...");
                        EmbedBuilder updatedEmbed = new EmbedBuilder().WithTitle("GIF Generation in Progress...");
                        await foreach (var (base64String, isFinal, ETR) in stableSwarmAPI.CreateGifAsync(payload))
                        {
                            try
                            {
                                // Convert the base64 string to a byte array
                                byte[] imageData = Convert.FromBase64String(base64String);
                                using MemoryStream imageStream = new(imageData);
                                MemoryStream embedStream = new();
                                imageStream.Position = 0; // Ensure the stream position is at the beginning for all checks
                                string suffix = "";
                                // Read the necessary header bytes for the largest expected header
                                byte[] header = new byte[12];
                                if (imageStream.Length >= header.Length)
                                {
                                    imageStream.Read(header, 0, header.Length);
                                    imageStream.Position = 0; // Reset position if further operations are needed on the stream
                                    // Check if GIF
                                    if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
                                    {
                                        suffix = "gif";
                                        Console.WriteLine("GIF generated");
                                        embedStream = imageStream;
                                    }
                                    // Check if JPEG
                                    else if (header[0] == 0xFF && header[1] == 0xD8)
                                    {
                                        suffix = "jpeg";
                                        Console.WriteLine("JPEG generated");
                                        embedStream = imageStream;
                                        // resize the image to 1024x768
                                        // TODO: Add proper using statement for SixLabors.ImageSharp
                                        using SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(imageData);
                                        image.Mutate(i => i.Resize(1024, 768));
                                        embedStream = new MemoryStream();
                                        image.SaveAsJpeg(embedStream);
                                    }
                                    // Check for WebP
                                    else if (header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                                    {
                                        suffix = "gif";
                                        Console.WriteLine("WebP detected");
                                        try
                                        {
                                            // Load the WebP image directly from the byte array
                                            using (SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(imageData))
                                            {
                                                // Triple the dimensions of the image
                                                int newWidth = image.Width * 3;
                                                int newHeight = image.Height * 3;
                                                // Resize the image
                                                image.Mutate(x => x.Resize(newWidth, newHeight));
                                                // Configure the GIF encoder to handle animation if necessary
                                                GifEncoder encoder = new()
                                                {
                                                    ColorTableMode = GifColorTableMode.Global,  // Use global color table for better compression
                                                    Quantizer = new WebSafePaletteQuantizer(),  // Reduce the number of colors if necessary
                                                };
                                                // Save the image as GIF to the MemoryStream
                                                image.SaveAsGif(embedStream, encoder);
                                            }
                                            embedStream.Position = 0;  // Reset the position of the MemoryStream for reading
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"An error occurred during the WebP to GIF conversion: {ex.Message}");
                                        }
                                    }
                                    // Use the MemoryStream for attachment and message updating
                                    FileAttachment file = new(embedStream, $"new_image.{suffix}");
                                    updatedEmbed.WithImageUrl($"attachment://new_image.{suffix}");
                                    updatedEmbed.WithColor(Discord.Color.Red);
                                    updatedEmbed.WithDescription($"Estimated Time Remaining: {ETR}");
                                    await processingMessage.ModifyAsync(msg =>
                                    {
                                        msg.Embeds = new[] { updatedEmbed.Build() };
                                        msg.Content = isFinal ? "Final GIF generated:" : "Updating GIF...";
                                        msg.Attachments = new[] { file };
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"An error occurred: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }
    }
}
