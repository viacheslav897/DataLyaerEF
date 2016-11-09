using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using Solution.Entities;
using Solution.Entities.Enums;

namespace Solution.Context
{
    public interface IContextWrapper : IDisposable
    {
        TR ApplyChanges<TR>(TR root) where TR : BaseEntity;
        IEnumerable<TR> ApplyChanges<TR>(IEnumerable<TR> items) where TR : BaseEntity;
        TR ApplyChangesImmediately<TR>(TR root) where TR : BaseEntity;
        IEnumerable<TR> ApplyChangesImmediately<TR>(IEnumerable<TR> items) where TR : BaseEntity;
        void CommitTransaction(bool startNewTransaction = false);
        EntityState ConvertState(State state);
        int Count<TR>() where TR : class;
        void Detach(object entity);
        void DetachedExistingEntity(object obj);
        IQueryable<TR> Find<TR>(Expression<Func<TR, bool>> where) where TR : class;

        IQueryable<TR> Find<TR>(Expression<Func<TR, bool>> where, params Expression<Func<TR, object>>[] includes)
            where TR : class;

        TR First<TR>(Expression<Func<TR, bool>> where) where TR : class;

        TR FirstOrDefault<TR>(Expression<Func<TR, bool>> where, params Expression<Func<TR, object>>[] includes)
            where TR : class;

        IQueryable<TR> GetAll<TR>() where TR : class;
        DbConnection GetConnection();
        void SaveChanges();
        void SetConnectionString(string connectionString);
        void SetIdentityCommand();
        void SetIsolationLevel(IsolationLevel isolationLevel);
        void SetTransactionType(TransactionTypes transactionType);
        void SetUseTransaction(bool useTransaction);
        DbContext GetDbContext();
        void RemoveRange<TR>(IEnumerable<TR> entities) where TR : class;

        void RollBack();
    }
}