using System.ComponentModel.DataAnnotations.Schema;
using Solution.Entities.Enums;

namespace Solution.Entities
{
    public class BaseEntity : IObjectWithState
    {
        [NotMapped]
        public State EntityState { get; set; }
    }
}