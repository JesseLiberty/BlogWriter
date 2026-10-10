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

        Assert.Equal("Min", cut.Find("label[for='min-words']").TextContent.Trim());
        Assert.Equal("Max", cut.Find("label[for='max-words']").TextContent.Trim());
        Assert.Equal("1000", cut.Find("#min-words").GetAttribute("value"));
        Assert.Equal("2000", cut.Find("#max-words").GetAttribute("value"));
        Assert.Equal("numeric", cut.Find("#min-words").GetAttribute("inputmode"));
        Assert.Equal("numeric", cut.Find("#max-words").GetAttribute("inputmode"));
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

    [Fact]
    public void Component_DisablesMinAndMaxWhenDisabled()
    {
        IRenderedComponent<WordRangeInput> cut = Render<WordRangeInput>(parameters => parameters
            .Add(component => component.Disabled, true));

        Assert.True(cut.Find("#min-words").HasAttribute("disabled"));
        Assert.True(cut.Find("#max-words").HasAttribute("disabled"));
    }
}