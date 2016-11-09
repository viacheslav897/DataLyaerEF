using System;
using System.Collections.Generic;
using System.Linq;
using Solution.Entities;
using Solution.Repository;

namespace Solution.Business
{
    public abstract class ServiceBase<T> where T : BaseEntity
    {
        #region Constructors

        protected ServiceBase(IRepositoryDbBase<T> repository)
        {
            Repository = repository;
        }

        #endregion

        #region

        protected IRepositoryDbBase<T> Repository { get; set; }

        #endregion

        public IEnumerable<TR> RefreshEntriesDuringUpdate<TR>(IEnumerable<TR> updatedEntries,
            IEnumerable<TR> inBaseEntries, Action<TR> deleteAction, Func<TR, TR> addFunc)
        {
            var result = new List<TR>();

            var baseEntries = inBaseEntries as IList<TR> ?? inBaseEntries.ToList();
            for (var index = 0; index < baseEntries.Count; index++)
            {
                var entity = baseEntries[index];
                deleteAction(entity);
            }

            foreach (var entity in updatedEntries)
            {
                addFunc(entity);
                result.Add(entity);
            }

            return result;
        }
    }
}