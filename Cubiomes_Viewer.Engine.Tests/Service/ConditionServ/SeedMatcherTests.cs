using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

/// <summary>How the matcher combines what the evaluators report, with evaluators that follow a script.</summary>
public sealed class SeedMatcherTests
{
    private static readonly BlockPos _origin = new(0, 0);
    private static readonly BlockPos _near = new(10, 0);
    private static readonly BlockPos _far = new(100, 40);

    private readonly SeedMatcher _matcher = new();
    private readonly SearchSeedContext _context = new(new NoWorkerContext(), new SearchDefinition());

    [Theory]
    [InlineData(MatchStatus.Failed, SearchOutcome.Failed)]
    [InlineData(MatchStatus.Unknown, SearchOutcome.Undecided)]
    [InlineData(MatchStatus.Candidates, SearchOutcome.Undecided)]
    [InlineData(MatchStatus.Matched, SearchOutcome.Passed)]
    public void Evaluate_ConditionWithNothingRelativeToIt_FollowsItsStatus(MatchStatus status, SearchOutcome expected)
    {
        var plan = new SearchPlan([Node(new Scripted(status, _near))]);

        Assert.Equal(expected, _matcher.Evaluate(plan, _context, SearchPass.Full));
    }

    [Theory]
    [InlineData(MatchStatus.Matched, MatchStatus.Matched, SearchOutcome.Passed)]
    [InlineData(MatchStatus.Matched, MatchStatus.Unknown, SearchOutcome.Undecided)]
    [InlineData(MatchStatus.Unknown, MatchStatus.Failed, SearchOutcome.Failed)]
    [InlineData(MatchStatus.Failed, MatchStatus.Matched, SearchOutcome.Failed)]
    public void Evaluate_SeveralConditions_NeedsEachOfThem(MatchStatus first, MatchStatus second, SearchOutcome expected)
    {
        var plan = new SearchPlan([Node(new Scripted(first, _near)), Node(new Scripted(second, _near))]);

        Assert.Equal(expected, _matcher.Evaluate(plan, _context, SearchPass.Full));
    }

    [Fact]
    public void Evaluate_RootConditions_LookAroundTheWorldOrigin()
    {
        var root = new Scripted(MatchStatus.Matched, _near);

        _matcher.Evaluate(new SearchPlan([Node(root)]), _context, SearchPass.Full);

        Assert.Equal([_origin], root.References);
    }

    [Fact]
    public void Evaluate_AnyMatch_TriesEveryMatchUntilTheRelativeConditionsHold()
    {
        var child = new Scripted(reference => reference == _far ? MatchStatus.Matched : MatchStatus.Failed);
        var plan = new SearchPlan([Node(new Scripted(MatchStatus.Matched, _near, _far), MatchAnchor.AnyMatch, Node(child))]);

        Assert.Equal(SearchOutcome.Passed, _matcher.Evaluate(plan, _context, SearchPass.Full));
        Assert.Equal([_near, _far], child.References);
    }

    [Fact]
    public void Evaluate_AnyMatch_FailsWhenTheRelativeConditionsHoldAtNoMatch()
    {
        var plan = new SearchPlan([Node(new Scripted(MatchStatus.Matched, _near, _far), MatchAnchor.AnyMatch, Node(new Scripted(MatchStatus.Failed)))]);

        Assert.Equal(SearchOutcome.Failed, _matcher.Evaluate(plan, _context, SearchPass.Full));
    }

    [Fact]
    public void Evaluate_Nearest_StartsOnlyFromTheMatchClosestToTheReferencePoint()
    {
        var child = new Scripted(reference => reference == _far ? MatchStatus.Matched : MatchStatus.Failed);
        var plan = new SearchPlan([Node(new Scripted(MatchStatus.Matched, _far, _near), MatchAnchor.Nearest, Node(child))]);

        Assert.Equal(SearchOutcome.Failed, _matcher.Evaluate(plan, _context, SearchPass.Full));
        Assert.Equal([_near], child.References);
    }

    [Fact]
    public void Evaluate_Center_StartsFromTheMiddleOfAllMatches()
    {
        var child = new Scripted(MatchStatus.Matched, _origin);
        var plan = new SearchPlan([Node(new Scripted(MatchStatus.Matched, _near, _far, new BlockPos(-50, 20)), MatchAnchor.Center, Node(child))]);

        Assert.Equal(SearchOutcome.Passed, _matcher.Evaluate(plan, _context, SearchPass.Full));
        Assert.Equal([new BlockPos(20, 20)], child.References);
    }

    [Fact]
    public void Evaluate_Candidates_FailWhenTheRelativeConditionsFailAtEachOfThem()
    {
        var plan = new SearchPlan([Node(new Scripted(MatchStatus.Candidates, _near, _far), MatchAnchor.Nearest, Node(new Scripted(MatchStatus.Failed)))]);

        Assert.Equal(SearchOutcome.Failed, _matcher.Evaluate(plan, _context, SearchPass.Quick));
    }

    [Fact]
    public void Evaluate_Candidates_StayUndecidedEvenWhenTheRelativeConditionsHold()
    {
        var child = new Scripted(MatchStatus.Matched, _origin);
        var plan = new SearchPlan([Node(new Scripted(MatchStatus.Candidates, _near, _far), MatchAnchor.AnyMatch, Node(child))]);

        Assert.Equal(SearchOutcome.Undecided, _matcher.Evaluate(plan, _context, SearchPass.Quick));
        Assert.Equal([_near], child.References);
    }

    [Fact]
    public void Evaluate_CandidatesWithTheirMiddleAsAnchor_StayUndecidedWithoutLookingFurther()
    {
        var child = new Scripted(MatchStatus.Failed);
        var plan = new SearchPlan([Node(new Scripted(MatchStatus.Candidates, _near, _far), MatchAnchor.Center, Node(child))]);

        Assert.Equal(SearchOutcome.Undecided, _matcher.Evaluate(plan, _context, SearchPass.Quick));
        Assert.Empty(child.References);
    }

    [Fact]
    public void Evaluate_UnknownWithRelativeConditions_StaysUndecidedWithoutLookingFurther()
    {
        var child = new Scripted(MatchStatus.Failed);
        var plan = new SearchPlan([Node(new Scripted(MatchStatus.Unknown), MatchAnchor.AnyMatch, Node(child))]);

        Assert.Equal(SearchOutcome.Undecided, _matcher.Evaluate(plan, _context, SearchPass.Quick));
        Assert.Empty(child.References);
    }

    [Theory]
    [InlineData(MatchStatus.Failed, MatchStatus.Matched, SearchOutcome.Passed)]
    [InlineData(MatchStatus.Failed, MatchStatus.Failed, SearchOutcome.Failed)]
    [InlineData(MatchStatus.Failed, MatchStatus.Unknown, SearchOutcome.Undecided)]
    [InlineData(MatchStatus.Candidates, MatchStatus.Matched, SearchOutcome.Passed)]
    public void Evaluate_AnyOf_NeedsOnlyOneOfTheConditionsRelativeToIt(MatchStatus first, MatchStatus second, SearchOutcome expected)
    {
        var anyOf = new PlanNode(new AnyOfCondition { Id = 1 }, new AnyOfConditionEvaluator(), [Node(new Scripted(first, _near)), Node(new Scripted(second, _near))]);

        Assert.Equal(expected, _matcher.Evaluate(new SearchPlan([anyOf]), _context, SearchPass.Full));
    }

    [Theory]
    [InlineData(MatchStatus.Failed, false, 0)]
    [InlineData(MatchStatus.Matched, true, 0)]
    [InlineData(MatchStatus.Candidates, true, 1)]
    [InlineData(MatchStatus.Unknown, true, 1)]
    public void IsMatch_QuickPassDecides_SkipsTheFullPass(MatchStatus quick, bool expected, int fullPasses)
    {
        var condition = new Scripted((_, pass) => pass == SearchPass.Quick ? quick : MatchStatus.Matched);

        Assert.Equal(expected, _matcher.IsMatch(new SearchPlan([Node(condition)]), _context));
        Assert.Equal(fullPasses, condition.Passes.Count(pass => pass == SearchPass.Full));
    }

    [Fact]
    public void IsMatch_QuickPassUndecidedAndFullPassFails_IsNoMatch()
    {
        var condition = new Scripted((_, pass) => pass == SearchPass.Quick ? MatchStatus.Candidates : MatchStatus.Failed);

        Assert.False(_matcher.IsMatch(new SearchPlan([Node(condition)]), _context));
    }

    private static PlanNode Node(Scripted evaluator, MatchAnchor anchor = MatchAnchor.AnyMatch, params PlanNode[] children)
        => new(new SpawnCondition { Id = 1, Anchor = anchor }, evaluator, children);

    private sealed class Scripted : IConditionEvaluator
    {
        public ConditionDescriptor Descriptor { get; } = new()
        {
            ConditionType = typeof(SpawnCondition),
            Name = "Scripted",
            Description = "Reports what the test tells it to.",
            Create = id => new SpawnCondition { Id = id },
            Cost = 1,
        };

        public List<BlockPos> References { get; } = [];
        public List<SearchPass> Passes { get; } = [];

        private readonly Func<BlockPos, SearchPass, MatchStatus> _script;
        private readonly BlockPos[] _matches;

        public Scripted(MatchStatus status, params BlockPos[] matches)
            : this((_, _) => status, matches)
        {
        }

        public Scripted(Func<BlockPos, MatchStatus> script)
            : this((reference, _) => script(reference), [new BlockPos(0, 0)])
        {
        }

        public Scripted(Func<BlockPos, SearchPass, MatchStatus> script)
            : this(script, [new BlockPos(0, 0)])
        {
        }

        private Scripted(Func<BlockPos, SearchPass, MatchStatus> script, BlockPos[] matches)
        {
            _script = script;
            _matches = matches;
        }

        public IEnumerable<string> Validate(SearchCondition condition, SearchDefinition definition) => [];

        public MatchStatus Evaluate(SearchCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
        {
            References.Add(reference);
            Passes.Add(pass);
            matches.AddRange(_matches);
            return _script(reference, pass);
        }
    }

    private sealed class NoWorkerContext : IWorkerContext
    {
        public IWorldGenerator GetGenerator(WorldKey world) => throw new NotSupportedException("Scripted conditions do not generate worlds.");
    }
}
