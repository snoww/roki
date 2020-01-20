using System;
using System.IO;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace Roki.Modules.Xp.Extensions
{
    public static class XpDrawExtensions
    {
       private const int PfpLength = 300;
        private const int PfpOutlineX = 65;
        private const int PfpOutlineY = 40;

        private const int XpBarX = 475;
        private const int XpBarY = 130;
        private const int XpBarHeight = 70;
        private const int XpBarLength = 1025;

        private const int MaxUsernameLength = 965;
        
        public static Stream GenerateXpBar(Stream avatar, int currentXp, int xpCap, string totalXp, string level, string rank, 
            string username, string discrim, DateTimeOffset date)
        {
            using Image image = Image.Load("./data/xp/roki_xp.png");

            var transparentGray = new Rgba32(211, 211, 211, 220);
            
            var grayBackground = new RectangleF(PfpOutlineX + (PfpLength + 20) / 2, 15, 1360, 370);
            
            var pfpBackground = new RectangleF(PfpOutlineX, PfpOutlineY, PfpLength + 20, PfpLength + 20);
            var pfpOutline = new RectangleF(PfpOutlineX, PfpOutlineY, PfpLength + 20, PfpLength + 20);
            
            var xpBarOutline = new RectangleF(XpBarX, XpBarY, XpBarLength, XpBarHeight);
            var xpBarProgress = new RectangleF(XpBarX + 5, XpBarY + 5, (XpBarLength - 10) * ((float) currentXp / xpCap), XpBarHeight - 10);
            
            image.Mutate(x => x
                .Fill(new Color(transparentGray), grayBackground) 
                // profile pic
                .Fill(Color.White, pfpBackground)
                .Draw(Color.WhiteSmoke, 7, pfpOutline)
                .Fill(Color.HotPink, new RectangleF(75, 50, 300, 300)) // profile pic

                // xp bar
                .Draw(Color.HotPink, 5, xpBarOutline)
                .Fill(Color.HotPink, xpBarProgress));
            
            using (Image pfp = Image.Load(avatar))
            {
                pfp.Mutate(x => x.Resize(new Size(PfpLength, PfpLength)));
                image.Mutate(x => x.DrawImage(pfp, new Point(PfpOutlineX + 10, PfpOutlineY + 10), 1));
            }
            
            var fonts = new FontCollection();
            fonts.Install("./data/xp/DIN_REG.ttf");
            fonts.Install("./data/xp/DIN_MED.ttf");
            Font usernameFont = fonts.CreateFont("DIN", 60);
            Font discriminatorFont = fonts.CreateFont("DIN", 42);
            Font xpFont = fonts.CreateFont("DIN", 60);
            Font headerFont = fonts.CreateFont("DIN Medium", 40);
            Font bodyFont = fonts.CreateFont("DIN", 50);
            
            image.Mutate(x => x
                .DrawUsername(usernameFont, discriminatorFont, username, $"#{discrim}", new PointF(XpBarX, 100)) // username + discrim
                .DrawText(new TextGraphicsOptions(true) {HorizontalAlignment = HorizontalAlignment.Center},
                    $"XP: {currentXp} / {xpCap}", xpFont, Color.DarkSlateGray, new PointF(XpBarX + XpBarLength / 2, 139)) // xp progress
                .DrawStats(headerFont, bodyFont, $"#{rank}", level, totalXp, date));
            
            var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Position = 0;
            return stream;
        }
        
        private static IImageProcessingContext DrawUsername(this IImageProcessingContext source, 
            Font userFont, Font discFont, string username, string discrim, PointF location)
        {
            var options = new TextGraphicsOptions(true)
            {
                ApplyKerning = true,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var userOptions = new RendererOptions(userFont, location)
            {
                ApplyKerning = options.ApplyKerning,
                VerticalAlignment = options.VerticalAlignment
            };
            
            IPathCollection userGlyphs = TextBuilder.GenerateGlyphs(username, userOptions);

            while (userGlyphs.Bounds.Width > MaxUsernameLength)
            {
                var trimmedUser = new StringBuilder();
                trimmedUser.Append(username.Remove(username.Length - 1));
                username = trimmedUser.ToString();
                trimmedUser.Append("...");
                userGlyphs = TextBuilder.GenerateGlyphs(trimmedUser.ToString(), userOptions);
            }

            var discrimLocation = new PointF(userGlyphs.Bounds.Right + 5, location.Y);
            var discrimOptions = new RendererOptions(discFont, discrimLocation)
            {
                ApplyKerning = options.ApplyKerning,
                VerticalAlignment = options.VerticalAlignment,
            };
            
            IPathCollection discrimGlyphs = TextBuilder.GenerateGlyphs(discrim, discrimOptions);

            var pathOptions = (GraphicsOptions) options;

            source.Fill(pathOptions, Brushes.Solid(Color.DarkSlateGray), userGlyphs);
            source.Fill(pathOptions, Brushes.Solid(Color.DarkSlateGray), discrimGlyphs);

            return source;
        }
        
        private static IImageProcessingContext DrawStats(this IImageProcessingContext source, Font header, Font body, 
            string rank, string level, string total, DateTimeOffset llup)
        {
            const float spacing = XpBarLength / 4f;
            const int y = 235;
            
            source.DrawLines(Color.HotPink, 5, new PointF(XpBarX + spacing, y), new PointF(XpBarX + spacing, y + 105))
                .DrawLines(Color.HotPink, 5, new PointF(XpBarX + spacing * 2, y), new PointF(XpBarX + spacing * 2, y + 105))
                .DrawLines(Color.HotPink, 5, new PointF(XpBarX + spacing * 3, y), new PointF(XpBarX + spacing * 3, y + 105));

            var headerOptions = new TextGraphicsOptions(true)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var bodyOptions = new TextGraphicsOptions(true)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
            };

            source.DrawText(headerOptions, "Rank", header, Color.HotPink, new PointF(603, y)) // (XpBarX + spacing) / 2
                .DrawText(bodyOptions, rank, body, Color.DarkSlateGray, new PointF(603, 340))
                
                .DrawText(headerOptions, "Level", header, Color.HotPink, new PointF(859, y)) // ((XpBarX + spacing) / 2 + (XpBarX + spacing * 2)) / 2
                .DrawText(bodyOptions, level, body, Color.DarkSlateGray, new PointF(859, 340))
                
                .DrawText(headerOptions, "Total XP", header, Color.HotPink, new PointF(1116, y))
                .DrawText(bodyOptions, total, body, Color.DarkSlateGray, new PointF(1116, 340))
                
                .DrawText(headerOptions, "Last Level Up", header, Color.HotPink, new PointF(1390, y))
                .DrawText(bodyOptions, $"{llup:MMM dd yyyy}", body, Color.DarkSlateGray, new PointF(1390, 340));

            return source;
        } 
    }
}