namespace Cubiomes_Viewer.Engine.Data.Compute;

public enum JobPriorityClass
{
    Interactive = 0,
    Visible = 1,
    Background = 2,
}

/// <summary>Jobs run by class first, then by ascending <see cref="Order"/> within a class.</summary>
public readonly record struct JobPriority(JobPriorityClass Class, long Order = 0) : IComparable<JobPriority>
{
    public int CompareTo(JobPriority other)
    {
        var byClass = Class.CompareTo(other.Class);
        return byClass != 0 ? byClass : Order.CompareTo(other.Order);
    }
}
