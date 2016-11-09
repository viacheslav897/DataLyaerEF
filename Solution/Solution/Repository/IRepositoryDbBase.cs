using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using Solution.Entities;

namespace Solution.Repository
{
    public interface IRepositoryDbBase<T> : IDisposable
        where T : BaseEntity
    {
        #region Public Methods and Operators

        T Add(T entity, params Expression<Func<T, object>>[] include);

        IEnumerable<T> AddRange(IEnumerable<T> entities);

        IEnumerable<T> UpdateRange(IEnumerable<T> entities);

        T AddImmediately(T entity, params Expression<Func<T, object>>[] include);

        void CommitTransaction();

        void RollbackTransaction();

        void Delete(T entity, params Expression<Func<T, object>>[] include);

        IQueryable<T> GetAll(params Expression<Func<T, object>>[] include);

        T Update(T entity, params Expression<Func<T, object>>[] include);

        DbContext GetDbContext();

        IEnumerable<RT> SqlQuery<RT>(string query);

        void RemoveRange(IEnumerable<T> entities);

        #endregion
    }
}