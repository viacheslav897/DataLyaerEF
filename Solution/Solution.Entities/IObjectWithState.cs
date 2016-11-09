using Solution.Entities.Enums;

namespace Solution.Entities
{
    public interface IObjectWithState
    {
        State EntityState { get; set; }
    }
}