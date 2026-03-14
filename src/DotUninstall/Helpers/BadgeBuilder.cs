using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;

namespace DotUninstall.Helpers;

public static class BadgeBuilder
{
    private const double DefaultFontSize = 11;

    private static PathGeometry _pathInfo = PathGeometry.Parse(@"
M12.713,16.713C12.904,16.521,13,16.283,13,16v-4c0-0.283-0.096-0.521-0.287-0.712
	C12.521,11.096,12.283,11,12,11s-0.521,0.096-0.712,0.288C11.096,11.479,11,11.717,11,12v4c0,0.283,0.096,0.521,0.288,0.713
	C11.479,16.904,11.717,17,12,17S12.521,16.904,12.713,16.713z 

M12.713,8.712C12.904,8.521,13,8.283,13,8s-0.096-0.521-0.287-0.712
	C12.521,7.096,12.283,7,12,7s-0.521,0.096-0.712,0.288C11.096,7.479,11,7.717,11,8s0.096,0.521,0.288,0.712
	C11.479,8.904,11.717,9,12,9S12.521,8.904,12.713,8.712z 

M12,22c-1.383,0-2.683-0.263-3.9-0.787
	c-1.217-0.525-2.275-1.238-3.175-2.138c-0.9-0.9-1.612-1.958-2.137-3.175S2,13.383,2,12c0-1.383,0.263-2.684,0.788-3.9
	c0.525-1.217,1.237-2.275,2.137-3.175c0.9-0.9,1.958-1.612,3.175-2.137S10.617,2,12,2c1.383,0,2.684,0.263,3.9,0.788
	s2.274,1.237,3.175,2.137c0.899,0.9,1.612,1.958,2.138,3.175C21.737,9.316,22,10.617,22,12c0,1.383-0.263,2.684-0.787,3.9
	c-0.525,1.217-1.238,2.274-2.138,3.175c-0.9,0.899-1.958,1.612-3.175,2.138C14.684,21.737,13.383,22,12,22z

M12,20
	c2.233,0,4.125-0.775,5.675-2.325S20,14.233,20,12s-0.775-4.125-2.325-5.675C16.125,4.775,14.233,4,12,4S7.875,4.775,6.325,6.325
	C4.775,7.875,4,9.767,4,12s0.775,4.125,2.325,5.675C7.875,19.225,9.767,20,12,20z");

    public static FrameworkElement TwoPartBadge(
        string label, string? value,
        Func<Color> labelBg, Func<Color> valueBg,
        string? infoUrl = null,
        string? toolTip = null,
        double fontSize = DefaultFontSize)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Border().Width(0).Height(0);

        var labelPart = new Border()
            .MinHeight(20)
            .WithTheme((t, c) => c.Background(labelBg()))
            .Padding(5, 2)
            .Child(
                new Label()
                    .Text(label)
                    .FontSize(fontSize)
                    .SemiBold()
                    .Foreground(ThemeColors.White)
            );

        var valueChildren = new List<FrameworkElement>
        {
            new Label()
                .Text(value)
                .FontSize(fontSize)
                .Foreground(ThemeColors.White)
        };

        if (!string.IsNullOrWhiteSpace(infoUrl))
        {
            valueChildren.Add(
                new Button()
                    .Width(14).Height(14).MinHeight(14)
                    .Padding(0)
                    .BorderThickness(0)
                    .Margin(2, 0, 0, 0)
                    .Background(Color.Transparent)
                    .OnClick(() => UrlLauncher.Open(infoUrl))
                    .Content(
                        new PathShape()
                            .Stretch(Stretch.Fill)
                            .Size(12, 12)
                            .Center()
                            .Fill(ThemeColors.White)
                            .Data(_pathInfo)
                    )
            );
        }

        var valuePart = new Border()
            .MinHeight(20)
            .WithTheme((t, c) => c.Background(valueBg()))
            .Padding(5, 2)
            .Child(
                new StackPanel()
                    .Horizontal()
                    .Spacing(2)
                    .CenterVertical()
                    .Children(valueChildren.ToArray())
            );

        return new Border()
            .BorderThickness(0)
            .ClipToBounds()
            .CornerRadius(4)
            .Child(new StackPanel()
                .Horizontal()
                .CenterVertical()
                .Children(labelPart, valuePart));
    }

    public static FrameworkElement SingleBadge(string? text, Func<Color> background, double fontSize = DefaultFontSize)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new Border().Width(0).Height(0);

        return new Border()
            .CornerRadius(4)
            .MinHeight(20)
            .WithTheme((t, c) => c.Background(background()))
            .Padding(5, 2)
            .CenterVertical()
            .Child(
                new Label()
                    .Text(text)
                    .FontSize(fontSize)
                    .Foreground(ThemeColors.White)
            );
    }
}
