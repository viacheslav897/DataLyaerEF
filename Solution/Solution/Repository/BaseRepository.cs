using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using Solution.Context;
using Solution.Entities;
using Solution.Entities.Enums;

namespace Solution.Repository
{
    public class BaseRepository<T> : IRepositoryDbBase<T>
        where T : BaseEntity
    {
        #region Constructors and Destructors

        public BaseRepository(IContextWrapper contextBase)
        {
            ContextBase = contextBase;
        }

        #endregion

        #region Properties

        protected IContextWrapper ContextBase { get; set; }

        #endregion

        #region Public Methods and Operators

        public T Add(T entity, params Expression<Func<T, object>>[] include)
        {
            return SetStateForEntities(entity, include, State.Added);
        }

        public IEnumerable<T> AddRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                var stateRoot = (IObjectWithState)entity;
                stateRoot.EntityState = stateRoot.EntityState == State.Unchanged ? State.Added : stateRoot.EntityState;
            }

            return ContextBase.ApplyChanges(entities);
        }

        public IEnumerable<T> UpdateRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                var stateRoot = (IObjectWithState)entity;
                stateRoot.EntityState = stateRoot.EntityState == State.Unchanged ? State.Modified : stateRoot.EntityState;
            }

            return ContextBase.ApplyChanges(entities);
        }

        public T AddImmediately(T entity, params Expression<Func<T, object>>[] include)
        {
            return SetStateForEntities(entity, include, State.Added, false);
        }

        public void CommitTransaction()
        {
            ContextBase.CommitTransaction();
        }

        public void RollbackTransaction()
        {
            ContextBase.RollBack();
        }

        public void Delete(T entity, params Expression<Func<T, object>>[] include)
        {
            SetStateForEntities(entity, include, State.Deleted);
        }

        public void Dispose()
        {
            ContextBase.Dispose();
        }

        public virtual IQueryable<T> GetAll(params Expression<Func<T, object>>[] include)
        {
            var result = ContextBase.GetAll<T>();
            if (include != null)
            {
                result = include.Aggregate(result, (current, expression) => current.Include(expression));
            }
            return result;
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            ContextBase.RemoveRange(entities);
        }

        public T Update(T entity, params Expression<Func<T, object>>[] include)
        {
            return SetStateForEntities(entity, include, State.Modified);
        }

        public DbContext GetDbContext()
        {
            return ContextBase.GetDbContext();
        }

        public IEnumerable<RT> SqlQuery<RT>(string query)
        {
            return ContextBase.GetDbContext().Database.SqlQuery<RT>(query).ToList();
        }

        #endregion

        #region Methods

        private T ApplyChanges(T root)
        {
            return ContextBase.ApplyChanges(root);
        }

        private T ApplyChangesImmediately(T root)
        {
            return ContextBase.ApplyChangesImmediately(root);
        }

        protected Type GetEnumerableType(Type type)
        {
            foreach (Type intType in type.GetInterfaces())
            {
                if (intType.IsGenericType && intType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return intType.GetGenericArguments()[0];
                }
            }

            return null;
        }

        private T SetStateForEntities(T obj, IEnumerable<Expression<Func<T, object>>> include, State state, bool useTransaction = true)
        {
            foreach (var property in include)
            {
                object value = property.Compile()(obj);
                Type listType = GetEnumerableType(value.GetType());
                if (listType != null)
                {
                    var listProperty = value as IEnumerable;
                    if (listProperty != null)
                    {
                        foreach (object prop in listProperty)
                        {
                            var stateProp = (IObjectWithState)prop;
                            stateProp.EntityState = stateProp.EntityState == State.Unchanged ? state : stateProp.EntityState;
                        }
                    }
                }
                else
                {
                    var stateValue = (IObjectWithState)value;
                    stateValue.EntityState = stateValue.EntityState == State.Unchanged ? state : stateValue.EntityState;
                }
            }

            var stateRoot = (IObjectWithState)obj;
            stateRoot.EntityState = stateRoot.EntityState == State.Unchanged ? state : stateRoot.EntityState;

            if (useTransaction)
                return ApplyChanges(obj);

            return ApplyChangesImmediately(obj);
        }

        #endregion
    }
}
