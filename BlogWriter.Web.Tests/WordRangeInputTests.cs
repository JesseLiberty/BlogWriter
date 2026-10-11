using Bunit;
using BlogWriter.Web.Components;

namespace BlogWriter.Web.Tests;

public sealed class WordRangeInputTests : BunitContext
{
    [Fact]
    public void Component_RendersExactLabelsValuesAndNumericHints()
    {
        IRenderedComponent<WordRangeInput> cut = Render<WordRangeInput>(parameters => parameters
            .Add(component => component.MinValue, "1000")
            .Add(component => component.MaxValue, "2000"));

        Assert.Equal("Target Words", cut.Find("#word-range-heading").TextContent.Trim());
        Assert.Equal("Minimum", cut.Find("label[for='min-words']").TextContent.Trim());
        Assert.Equal("Maximum", cut.Find("label[for='max-words']").TextContent.Trim());
        Assert.Empty(cut.FindAll(".range-separator"));
        Assert.Equal(new[] { "word-range-heading", "", "min-words", "", "max-words" },
            cut.FindAll("#word-range-heading, label, input").Select(element => element.Id).ToArray());
        Assert.Equal("1000", cut.Find("#min-words").GetAttribute("value"));
        Assert.Equal("2000", cut.Find("#max-words").GetAttribute("value"));
        Assert.Equal("numeric", cut.Find("#min-words").GetAttribute("inputmode"));
        Assert.Equal("numeric", cut.Find("#max-words").GetAttribute("inputmode"));
    }

    [Fact]
    public void Component_PreservesValueCallbacksAndDisabledState()
    {
        string? min = null;
        string? max = null;
        var cut = Render<WordRangeInput>(parameters => parameters
            .Add(component => component.MinValueChanged, (string value) => min = value)
            .Add(component => component.MaxValueChanged, (string value) => max = value));
        cut.Find("#min-words").Input("700");
        cut.Find("#max-words").Input("1350");
        Assert.Equal("700", min);
        Assert.Equal("1350", max);
        var disabled = Render<WordRangeInput>(parameters => parameters.Add(component => component.Disabled, true));
        Assert.All(disabled.FindAll("input"), input => Assert.True(input.HasAttribute("disabled")));
    }

    [Fact]
    public void Component_AssociatesErrorsWithInvalidFields()
    {
        IRenderedComponent<WordRangeInput> cut = Render<WordRangeInput>(parameters => parameters
            .Add(component => component.MinValue, "0")
            .Add(component => component.MaxValue, "500")
            .Add(component => component.MinError, "Min must be a positive whole number."));

        Assert.Equal("true", cut.Find("#min-words").GetAttribute("aria-invalid"));
        Assert.Contains("min-words-error", cut.Find("#min-words").GetAttribute("aria-describedby"));
        Assert.Equal("Min must be a positive whole number.", cut.Find("#min-words-error").TextContent.Trim());
    }
}