using System.Windows;
using VK03Panel.Core.Config;
using VK03Panel.Core.Themes;
using Xunit;

namespace VK03Panel.Core.Tests;

public sealed class ThemeLayoutTests
{
    [Theory]
    [InlineData(PanelThemes.Win11Id)]
    [InlineData(PanelThemes.MacId)]
    public void TouchTargetsStaySeparateAndHaveUsableEdges(string themeId)
    {
        var theme = PanelThemes.Get(themeId);
        var actions = Enum.GetValues<PanelAction>().Where(action => !theme.Bounds(action).IsEmpty).ToArray();
        foreach (var action in actions)
        {
            var bounds = theme.Bounds(action);
            Assert.True(bounds.Width >= 48 && bounds.Height >= 48);
            foreach (var other in actions.Where(other => other != action))
                Assert.False(bounds.IntersectsWith(theme.Bounds(other)), $"{action} overlaps {other}");
            // A finger landing near any edge must trigger the visible control.
            foreach (var x in new[] { bounds.Left + 1, bounds.Right - 1 })
            foreach (var y in new[] { bounds.Top + 1, bounds.Bottom - 1 })
                Assert.Equal(action, theme.HitTest(new Point(x, y)));
        }
    }

    [Theory]
    [InlineData(PanelThemes.Win11Id)]
    [InlineData(PanelThemes.MacId)]
    public void EveryActionHasUsableBounds(string themeId)
    {
        var theme = PanelThemes.Get(themeId);
        foreach (var action in Enum.GetValues<PanelAction>())
        {
            if (action == PanelAction.None)
            {
                continue;
            }

            var bounds = theme.Bounds(action);
            if (bounds.IsEmpty)
            {
                // The Windows 11 layout intentionally omits the ± buttons; the slider covers it.
                Assert.True(themeId == PanelThemes.Win11Id &&
                            action is PanelAction.Quieter or PanelAction.Louder);
                continue;
            }

            Assert.True(bounds.Width > 0 && bounds.Height > 0);
            Assert.True(bounds.Right <= theme.DesignSize.Width);
            Assert.True(bounds.Bottom <= theme.DesignSize.Height);
        }
    }

    [Theory]
    [InlineData(PanelThemes.Win11Id)]
    [InlineData(PanelThemes.MacId)]
    public void HitTest_ResolvesCenterOfEachAction(string themeId)
    {
        var theme = PanelThemes.Get(themeId);
        foreach (var action in new[]
                 {
                     PanelAction.Mute, PanelAction.Previous, PanelAction.Play,
                     PanelAction.Next, PanelAction.Lock,
                 })
        {
            var bounds = theme.Bounds(action);
            var center = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
            Assert.Equal(action, theme.HitTest(center));
        }
    }

    [Theory]
    [InlineData(PanelThemes.Win11Id)]
    [InlineData(PanelThemes.MacId)]
    public void SliderSitsInsideVolumeBounds(string themeId)
    {
        var theme = PanelThemes.Get(themeId);
        var volume = theme.Bounds(PanelAction.Volume);
        var sliderStart = new Point(theme.SliderLeft, volume.Y + (volume.Height / 2));
        var sliderEnd = new Point(theme.SliderLeft + theme.SliderWidth, volume.Y + (volume.Height / 2));
        Assert.True(volume.Contains(sliderStart));
        Assert.True(volume.Contains(sliderEnd));
        Assert.Equal(PanelAction.Volume, theme.HitTest(sliderEnd));
    }

    [Fact]
    public void HitTest_ReturnsNullOutsidePanel()
    {
        var theme = PanelThemes.Get(PanelThemes.Win11Id);
        Assert.Null(theme.HitTest(new Point(5, 5)));
        Assert.Null(theme.HitTest(new Point(959, 359)));
    }
}

public sealed class PanelConfigTests
{
    [Fact]
    public void Sanitize_RepairsInvalidValues()
    {
        var config = new PanelConfig
        {
            ThemeId = "unknown",
            Appearance = "neon",
            Accent = "rainbow",
            AccentHex = "not-a-colour",
            Rotation = 45,
            RefreshPerSecond = 99,
        };

        config.Sanitize();

        Assert.Equal("win11", config.ThemeId);
        Assert.Equal("system", config.Appearance);
        Assert.Equal("system", config.Accent);
        Assert.Equal("#0078D4", config.AccentHex);
        Assert.Equal(90, config.Rotation);
        Assert.Equal(2, config.RefreshPerSecond);
    }

    [Fact]
    public void Clone_CopiesEveryField()
    {
        var config = new PanelConfig
        {
            ThemeId = "mac",
            Appearance = "light",
            Accent = "fixed",
            AccentHex = "#00B294",
            Rotation = 270,
            StartWithWindows = true,
            StartMinimized = true,
            TurnOffPanelOnExit = false,
            RefreshPerSecond = 2,
        };

        var clone = config.Clone();
        Assert.Equal("mac", clone.ThemeId);
        Assert.Equal("light", clone.Appearance);
        Assert.Equal("fixed", clone.Accent);
        Assert.Equal("#00B294", clone.AccentHex);
        Assert.Equal(270, clone.Rotation);
        Assert.True(clone.StartWithWindows);
        Assert.True(clone.StartMinimized);
        Assert.False(clone.TurnOffPanelOnExit);
        Assert.Equal(2, clone.RefreshPerSecond);
    }
}
