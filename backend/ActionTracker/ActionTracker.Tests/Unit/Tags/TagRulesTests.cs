using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Validators;
using ActionTracker.Application.Features.Tags;
using FluentAssertions;

namespace ActionTracker.Tests.Unit.Tags;

/// <summary>
/// WO-2026-11 A-5 tag rules (Q14 + D6). The client copy
/// (<c>tag-rules.util.spec.ts</c>) repeats these cases.
/// </summary>
public class TagRulesTests
{
    [Theory]
    [InlineData("  Urgent  ", "Urgent")]
    [InlineData("Q4   review", "Q4 review")]
    [InlineData("a\tb\nc", "a b c")]
    [InlineData(null, "")]
    public void Clean_trims_and_collapses_white_space(string? raw, string expected) =>
        TagRules.Clean(raw).Should().Be(expected);

    [Fact]
    public void Normalize_ignores_case() =>
        TagRules.Normalize("Ünit Q4").Should().Be(TagRules.Normalize("üNIT q4"));

    [Fact]
    public void Null_means_leave_the_tags_and_is_fine() =>
        TagRules.Problem(null).Should().BeNull();

    [Theory]
    [InlineData("",        "A tag cannot be empty.")]
    [InlineData("   ",     "A tag cannot be empty.")]
    [InlineData("a,b",     "Tag \"a,b\" cannot contain a comma.")]
    [InlineData("bell\u0007", "Tag \"bell?\" contains a character that is not allowed.")]
    [InlineData("1234567890123456789012345678901", "Tag \"1234567890123456789012345678901\" is longer than 30 characters.")]
    public void Refused_tags_name_the_reason(string tag, string message) =>
        TagRules.Problem(new[] { "ok", tag }).Should().Be(message);

    [Fact]
    public void Thirty_characters_after_cleaning_are_fine() =>
        TagRules.Problem(new[] { "  " + new string('x', 30) + "  " }).Should().BeNull();

    [Fact]
    public void At_most_ten_distinct_tags_duplicates_do_not_count()
    {
        var ten = Enumerable.Range(1, 10).Select(i => $"t{i}").ToList();
        TagRules.Problem(ten.Append("T1").Append(" t2 ")).Should().BeNull();
        TagRules.Problem(ten.Append("t11")).Should().Be("An action item can have at most 10 tags.");
    }

    [Fact]
    public void CleanAll_keeps_the_order_and_the_first_spelling()
    {
        TagRules.CleanAll(new[] { " Urgent ", "q4", "URGENT", "Q4  ", "Board  pack" })
            .Should().Equal("Urgent", "q4", "Board pack");
    }

    [Fact]
    public void CleanAll_throws_the_rule_as_a_400()
    {
        var act = () => TagRules.CleanAll(new[] { "a,b" });
        act.Should().Throw<TagRuleException>().WithMessage("Tag \"a,b\" cannot contain a comma.")
            .Which.Should().BeAssignableTo<ArgumentException>();
    }

    [Fact]
    public void The_validators_apply_the_same_rules()
    {
        var create = new ActionItemCreateValidator().Validate(new ActionItemCreateDto
        {
            Title = "T", WorkspaceId = Guid.NewGuid(), AssigneeIds = ["u"], DueDate = DateTime.UtcNow,
            Tags = ["fine", "not,fine"],
        });
        create.IsValid.Should().BeFalse();
        create.Errors.Should().ContainSingle(e => e.ErrorMessage == "Tag \"not,fine\" cannot contain a comma.");

        var update = new ActionItemUpdateValidator().Validate(new ActionItemUpdateDto
        {
            Id = Guid.NewGuid(), Tags = Enumerable.Range(1, 11).Select(i => $"t{i}").ToList(),
        });
        update.Errors.Should().ContainSingle(e => e.ErrorMessage == "An action item can have at most 10 tags.");

        new ActionItemUpdateValidator().Validate(new ActionItemUpdateDto { Id = Guid.NewGuid() }).IsValid.Should().BeTrue();
        new ActionItemUpdateValidator().Validate(new ActionItemUpdateDto { Id = Guid.NewGuid(), Tags = [] }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_payload_with_tags_is_not_an_actual_dates_only_payload()
    {
        // A Done item accepts only actual dates (A-1); tags are refused there like any other field (D1).
        new ActionItemUpdateDto { Id = Guid.NewGuid(), ActualEndDate = DateTime.UtcNow }.ChangesOnlyActualDates().Should().BeTrue();
        new ActionItemUpdateDto { Id = Guid.NewGuid(), ActualEndDate = DateTime.UtcNow, Tags = ["x"] }.ChangesOnlyActualDates().Should().BeFalse();
        new ActionItemUpdateDto { Id = Guid.NewGuid(), Tags = [] }.ChangesOnlyActualDates().Should().BeFalse();
    }
}
