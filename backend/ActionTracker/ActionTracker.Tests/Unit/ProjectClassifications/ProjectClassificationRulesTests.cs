using ActionTracker.Application.Features.ProjectClassifications;
using FluentAssertions;

namespace ActionTracker.Tests.Unit.ProjectClassifications;

/// <summary>
/// WO-2026-11 P-1 name rules. The client copy
/// (<c>project-classification-rules.util.spec.ts</c>) repeats these cases —
/// change both together.
/// </summary>
public class ProjectClassificationRulesTests
{
    [Theory]
    [InlineData("  Capital  works ", "Capital works")]
    [InlineData("Research\t\tgrant", "Research grant")]
    [InlineData("Line\nbreak", "Line break")]
    [InlineData("Already clean", "Already clean")]
    [InlineData(null, "")]
    public void Clean_trims_and_collapses_white_space(string? raw, string expected) =>
        ProjectClassificationRules.Clean(raw).Should().Be(expected);

    [Fact]
    public void Normalize_ignores_case() =>
        ProjectClassificationRules.Normalize("Capital Works")
            .Should().Be(ProjectClassificationRules.Normalize("CAPITAL works"));

    [Theory]
    [InlineData(null, "Enter a name for the classification.")]
    [InlineData("", "Enter a name for the classification.")]
    [InlineData("   ", "Enter a name for the classification.")]
    [InlineData("Bad\u0007bell", "The classification name contains a character that is not allowed.")]
    public void NameProblem_names_the_broken_rule(string? raw, string expected) =>
        ProjectClassificationRules.NameProblem(raw).Should().Be(expected);

    [Fact]
    public void A_name_of_100_characters_is_accepted_and_101_refused()
    {
        ProjectClassificationRules.NameProblem(new string('a', 100)).Should().BeNull();
        ProjectClassificationRules.NameProblem(new string('a', 101))
            .Should().Be("The classification name can be at most 100 characters.");
        // Length counts after cleaning: outer spaces do not count.
        ProjectClassificationRules.NameProblem("  " + new string('a', 100) + "  ").Should().BeNull();
    }

    [Fact]
    public void Commas_and_punctuation_are_allowed() =>
        ProjectClassificationRules.NameProblem("Teaching, learning & research (TLR)").Should().BeNull();

    [Fact]
    public void CleanOrThrow_returns_the_clean_name_or_throws_ArgumentException()
    {
        ProjectClassificationRules.CleanOrThrow(" Capital   works ").Should().Be("Capital works");
        var act = () => ProjectClassificationRules.CleanOrThrow(" ");
        act.Should().Throw<ArgumentException>().WithMessage("Enter a name for the classification.");
    }
}
