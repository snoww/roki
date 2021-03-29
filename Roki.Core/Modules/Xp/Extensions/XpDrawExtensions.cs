using System;
using System.Globalization;
using System.IO;
using System.Text;
using Roki.Modules.Xp.Common;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Roki.Modules.Xp.Extensions
{
    public static class XpDrawExtensions
    {
        private const int PfpLength = 150;
        private const int PfpOutlineX = 32;
        private const int PfpOutlineY = 20;

        private const int XpBarX = 238;
        private const int XpBarY = 65;
        private const int XpBarHeight = 35;
        private const int XpBarLength = 512;

        private const int MaxUsernameLength = 480;

        public static MemoryStream GenerateXpBar(Stream avatar, XpLevel xp, string rank,
            string username, string discriminator, DateTime date, bool doubleXp, bool fastXp)
        {
            using Image image = Image.Load("data/xp/roki_xp_half.png");

            var transparentGray = new Rgba32(211, 211, 211, 220);

            var grayBackground = new RectangleF(PfpOutlineX + (PfpLength + 10) / 2, 7.5f, 680, 185);

            var pfpBackground = new RectangleF(PfpOutlineX, PfpOutlineY, PfpLength + 10, PfpLength + 10);
            var pfpOutline = new RectangleF(PfpOutlineX, PfpOutlineY, PfpLength + 10, PfpLength + 10);

            var xpBarOutline = new RectangleF(XpBarX, XpBarY, XpBarLength, XpBarHeight);
            image.Mutate(x => x
                .Fill(new Color(transparentGray), grayBackground)
                // profile pic
                .Fill(Color.White, pfpBackground)
                .Draw(Color.WhiteSmoke, 3.5f, pfpOutline)
                .Fill(Color.HotPink, new RectangleF(37, 25, 150, 150)) // profile pic

                // xp bar
                .Draw(Color.HotPink, 2.5f, xpBarOutline));

            if (xp.TotalXp != 0)
            {
                var xpBarProgress = new RectangleF(XpBarX + 2.5f, XpBarY + 2.5f, (XpBarLength - 5) * ((float) xp.ProgressXp / xp.RequiredXp), XpBarHeight - 5);
                image.Mutate(x => x.Fill(Color.HotPink, xpBarProgress));
            }

            using (Image pfp = Image.Load(avatar))
            {
                pfp.Mutate(x => x.Resize(new Size(PfpLength, PfpLength)));
                image.Mutate(x => x.DrawImage(pfp, new Point(PfpOutlineX + 5, PfpOutlineY + 5), 1));
            }

            var fonts = new FontCollection();
            fonts.Install("data/xp/DIN_REG.ttf");
            fonts.Install("data/xp/DIN_MED.ttf");
            Font usernameFont = fonts.CreateFont("DIN", 30);
            Font discriminatorFont = fonts.CreateFont("DIN", 21);
            Font xpFont = fonts.CreateFont("DIN", 30);
            Font headerFont = fonts.CreateFont("DIN Medium", 20);
            Font bodyFont = fonts.CreateFont("DIN", 25);
            image.Mutate(x => x
                .DrawUsername(usernameFont, discriminatorFont, username, $"#{discriminator}", new PointF(XpBarX, 50)) // username + discrim
                .DrawText(new TextGraphicsOptions {TextOptions = {HorizontalAlignment = HorizontalAlignment.Center}},
                    $"XP: {xp.TotalXp:N0} / {xp.TotalRequiredXp:N0}", xpFont, Color.DarkSlateGray, new PointF(XpBarX + XpBarLength / 2, 66)) // xp progress
                .DrawStats(headerFont, bodyFont, $"#{rank}", xp.Level.ToString("N0"), (xp.TotalRequiredXp - xp.TotalXp).ToString("N0"), date)
                .DrawBoosts(doubleXp, fastXp));

            var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Position = 0;
            return stream;
        }

        private static IImageProcessingContext DrawUsername(this IImageProcessingContext source,
            Font userFont, Font discFont, string username, string discrim, PointF location)
        {
            var options = new TextGraphicsOptions {TextOptions = {ApplyKerning = true, VerticalAlignment = VerticalAlignment.Bottom}};

            var userOptions = new RendererOptions(userFont, location) {ApplyKerning = options.TextOptions.ApplyKerning, VerticalAlignment = options.TextOptions.VerticalAlignment};

            IPathCollection userGlyphs = TextBuilder.GenerateGlyphs(username, userOptions);

            while (userGlyphs.Bounds.Width > MaxUsernameLength)
            {
                var trimmedUser = new StringBuilder();
                trimmedUser.Append(username.Remove(username.Length - 1));
                username = trimmedUser.ToString();
                trimmedUser.Append("...");
                userGlyphs = TextBuilder.GenerateGlyphs(trimmedUser.ToString(), userOptions);
            }

            var discrimLocation = new PointF(userGlyphs.Bounds.Right + 2, location.Y);
            var discrimOptions = new RendererOptions(discFont, discrimLocation) {ApplyKerning = options.TextOptions.ApplyKerning, VerticalAlignment = options.TextOptions.VerticalAlignment};

            IPathCollection discrimGlyphs = TextBuilder.GenerateGlyphs(discrim, discrimOptions);

            source.Fill(Brushes.Solid(Color.DarkSlateGray), userGlyphs);
            source.Fill(Brushes.Solid(Color.DarkSlateGray), discrimGlyphs);

            return source;
        }

        private static IImageProcessingContext DrawStats(this IImageProcessingContext source, Font header, Font body,
            string rank, string level, string required, DateTime lastLevelUp)
        {
            const float spacing = XpBarLength / 4f;
            const float y = 117.5f;

            source.DrawLines(Color.HotPink, 2.5f, new PointF(XpBarX + spacing, y), new PointF(XpBarX + spacing, y + 52))
                .DrawLines(Color.HotPink, 2.5f, new PointF(XpBarX + spacing * 2, y), new PointF(XpBarX + spacing * 2, y + 52))
                .DrawLines(Color.HotPink, 2.5f, new PointF(XpBarX + spacing * 3, y), new PointF(XpBarX + spacing * 3, y + 52));

            var headerOptions = new TextGraphicsOptions {TextOptions = {HorizontalAlignment = HorizontalAlignment.Center}};

            var bodyOptions = new TextGraphicsOptions {TextOptions = {HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,}};

            // need to use en-US since en-CA adds a . to month abbreviation (en-US -> Jan, en-CA -> Jan.)
            string lastLevelUpString = lastLevelUp == DateTime.MinValue ? "Never" : lastLevelUp.ToString("MMM dd yyyy", new CultureInfo("en-US"));
            source.DrawText(headerOptions, "Rank", header, Color.HotPink, new PointF(301, y)) // (XpBarX + spacing) / 2
                .DrawText(bodyOptions, rank, body, Color.DarkSlateGray, new PointF(301, 170))
                .DrawText(headerOptions, "Level", header, Color.HotPink, new PointF(430, y)) // ((XpBarX + spacing) / 2 + (XpBarX + spacing * 2)) / 2
                .DrawText(bodyOptions, level, body, Color.DarkSlateGray, new PointF(430, 170))
                .DrawText(headerOptions, "Required XP", header, Color.HotPink, new PointF(558, y))
                .DrawText(bodyOptions, required, body, Color.DarkSlateGray, new PointF(558, 170))
                .DrawText(headerOptions, "Last Level Up", header, Color.HotPink, new PointF(695, y))
                .DrawText(bodyOptions, lastLevelUpString, body, Color.DarkSlateGray, new PointF(695, 170));

            return source;
        }

        private static IImageProcessingContext DrawBoosts(this IImageProcessingContext source, bool doubleXp, bool fastXp)
        {
            if (doubleXp && fastXp)
            {
                using Image<Rgba32> image = Image.Load<Rgba32>("data/xp/boost_both_half.png");
                source.DrawImage(image, new Point(667,  XpBarY), 1); // 1500 - 10 - width of boost_both.png
            }
            else if (doubleXp)
            {
                using Image<Rgba32> image = Image.Load<Rgba32>("data/xp/boost_double_half.png");
                source.DrawImage(image, new Point(710, XpBarY), 1);
            }
            else if (fastXp)
            {
                using Image<Rgba32> image = Image.Load<Rgba32>("data/xp/boost_fast_half.png");
                source.DrawImage(image, new Point(710, XpBarY + 3), 1);
            }

            return source;
        }
    }
}