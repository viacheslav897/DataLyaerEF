using System.Configuration;
using System.Data.Entity;

namespace Solution
{
    public class ApplicationContext : DbContext
    {
        public ApplicationContext() : 
            base(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString)
        {
            this.Configuration.LazyLoadingEnabled = false;
        }

        //Add DbSets
    }
}