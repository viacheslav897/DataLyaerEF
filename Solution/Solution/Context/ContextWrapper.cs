using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Transactions;
using Solution.Entities;
using Solution.Entities.Enums;
using IsolationLevel = System.Data.IsolationLevel;

namespace Solution.Context
{
    public class ContextWrapper<T> : IContextWrapper
        where T : DbContext
    {
        #region Deligates

        public delegate void RepositoryBaseExceptionHandler(Exception exception);

        public delegate void RepositoryBaseRollBackOccuredHandler(MethodBase lastExecutedMethod);

        #endregion

        #region Static Fields and Constants

        private const bool ProxyCreationEnabled = true;

        #endregion

        #region Fields

        private readonly Guid _index = Guid.NewGuid();
        private readonly bool _saveLastExcecutedMethodInfo;
        private DbConnection _connection;
        private string _connectionString;
        private DbContext _context;
        private bool _disposing;
        private IsolationLevel _isolationLevel = IsolationLevel.Unspecified;
        private bool _lazyConnection;
        private DbTransaction _transaction;
        private TransactionScope _transactionScope;
        private TransactionTypes _transactionType = TransactionTypes.DbTransaction;
        private bool _useTransaction = true;
        private readonly IPrincipal _principal;

        #endregion

        #region Constructors

        public ContextWrapper()
            : this(null)
        {
        }

        public ContextWrapper(IPrincipal principal)
        {
            Trace.TraceInformation("Creating {0}: {1}", typeof(T).Name, _index);
            _principal = principal;
            _saveLastExcecutedMethodInfo = false;
            InitializeRepository();
        }

        #endregion

        public IEnumerable<TR> ApplyChangesImmediately<TR>(IEnumerable<TR> items) where TR : BaseEntity
        {
            try
            {
                DetachAllUnchangedEntities();

                foreach (var item in items)
                {
                    if (IsProxy(item))
                    {
                        _context.Set<TR>().Attach(item);
                    }
                    else
                    {
                        _context.Set<TR>().Add(item);
                    }
                }

                SetStateForEntities();

                SaveChanges();

                SetUnchangedState();

                return items;
            }
            catch (Exception ex)
            {
                DetachList(items.ToList());
                throw;
            }
        }

        public TR ApplyChangesImmediately<TR>(TR root) where TR : BaseEntity
        {
            try
            {
                DetachAllUnchangedEntities();

                if (IsProxy(root))
                {
                    _context.Set<TR>().Attach(root);
                }
                else
                {
                    _context.Set<TR>().Add(root);
                }

                SetStateForEntities();

                SaveChanges();

                SetUnchangedState();

                return root;
            }
            catch (Exception)
            {
                Detach(root);
                throw;
            }
        }

        public TR ApplyChanges<TR>(TR root) where TR : BaseEntity
        {
            TR result = null;

            ProcessTransactionableMethod(
                () =>
                {
                    try
                    {
                        result = ApplyChangesImmediately(root);
                    }
                    catch (Exception)
                    {
                        RollBack();
                        throw;
                    }
                });

            return result;
        }

        public IEnumerable<TR> ApplyChanges<TR>(IEnumerable<TR> items) where TR : BaseEntity
        {
            IEnumerable<TR> result = null;

            ProcessTransactionableMethod(
                () =>
                {
                    try
                    {
                        result = ApplyChangesImmediately(items);
                    }
                    catch (DbEntityValidationException e)
                    {
                        foreach (var eve in e.EntityValidationErrors)
                        {
                            Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                                eve.Entry.Entity.GetType().Name, eve.Entry.State);
                            foreach (var ve in eve.ValidationErrors)
                            {
                                Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                                    ve.PropertyName, ve.ErrorMessage);
                            }
                        }
                        RollBack();
                        throw;
                    }
                    catch (Exception)
                    {
                        RollBack();
                        throw;
                    }
                });

            return result;
        }


        public DbContext GetDbContext()
        {
            return _context;
        }

        public void CommitTransaction(bool startNewTransaction = false)
        {
            Trace.TraceWarning("!!!Commit {0} - {1}", typeof(T).Name, _index);

            if (_useTransaction)
            {
                switch (_transactionType)
                {
                    case TransactionTypes.DbTransaction:
                        if (_transaction?.Connection != null)
                        {
                            _transaction.Commit();
                        }

                        break;

                    case TransactionTypes.TransactionScope:
                        _transactionScope?.Complete();
                        break;
                }

                if (startNewTransaction)
                {
                    StartTransaction();
                }
            }
        }

        public EntityState ConvertState(State state)
        {
            switch (state)
            {
                case State.Added:
                    return EntityState.Added;
                case State.Modified:
                    return EntityState.Modified;
                case State.Deleted:
                    return EntityState.Deleted;
                default:
                    return EntityState.Unchanged;
            }
        }

        public int Count<TR>() where TR : class
        {
            return _context.Set<TR>().Count();
        }

        public void Detach(object entity)
        {
            if (entity != null)
            {
                var objectContext = ((IObjectContextAdapter)_context).ObjectContext;
                var entry = _context.Entry(entity);

                if (entry.State != EntityState.Detached)
                {
                    objectContext.Detach(entity);
                }
            }
        }

        public void DetachList<TR>(List<TR> entities) where TR : BaseEntity
        {
            entities.ForEach(Detach);
        }

        public void DetachedExistingEntity(object obj)
        {
            var entries = _context.ChangeTracker.Entries<IObjectWithState>().ToArray();
            foreach (var entry in entries)
            {
                if (Equals(entry, obj))
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        public void Dispose()
        {
            if (_disposing)
            {
                return;
            }

            _disposing = true;
            /*try
            {
                CommitTransaction();
            }
            catch (Exception error)
            {
                RollBack();
                if (_rethrowExceptions)
                {
                    throw;
                }
            }*/

            _transaction = null;
            _transactionScope = null;

            DisposeContext(ref _context);
        }

        public IQueryable<TR> Find<TR>(Expression<Func<TR, bool>> where) where TR : class
        {
            var entities = default(IQueryable<TR>);

            ProcessTransactionableMethod(() => { entities = SetEntities<TR>().Where(where); });

            return entities;
        }

        public IQueryable<TR> Find<TR>(Expression<Func<TR, bool>> where, params Expression<Func<TR, object>>[] includes)
            where TR : class
        {
            var entities = SetEntities<TR>();

            ProcessTransactionableMethod(
                () =>
                {
                    if (includes != null)
                    {
                        entities = ApplyIncludesToQuery(entities, includes);
                    }

                    entities = entities.Where(where);
                });

            return entities;
        }

        public TR First<TR>(Expression<Func<TR, bool>> where) where TR : class
        {
            var entities = SetEntities<TR>();

            var entity = default(TR);

            ProcessTransactionableMethod(() => { entity = entities.First(where); });

            return entity;
        }

        public TR FirstOrDefault<TR>(Expression<Func<TR, bool>> where, params Expression<Func<TR, object>>[] includes)
            where TR : class
        {
            var entities = SetEntities<TR>();

            var entity = default(TR);

            ProcessTransactionableMethod(
                () =>
                {
                    if (where != null)
                    {
                        entities = entities.Where(where);
                    }

                    if (includes != null)
                    {
                        entities = ApplyIncludesToQuery(entities, includes);
                    }

                    entity = entities.FirstOrDefault();
                });

            return entity;
        }

        public IQueryable<TR> GetAll<TR>() where TR : class
        {
            var entities = SetEntities<TR>().AsNoTracking();

            return entities;
        }

        public DbConnection GetConnection()
        {
            return _connection;
        }

        public void SaveChanges()
        {
            _context?.SaveChanges();
        }

        public void RemoveRange<TR>(IEnumerable<TR> entities) where TR : class
        {
            ProcessTransactionableMethod(
                () =>
                {
                    try
                    {
                        DetachAllUnchangedEntities();

                        foreach (var entity in entities)
                        {
                            _context.Set<TR>().Attach(entity);
                        }

                        _context.Set<TR>().RemoveRange(entities);

                        SaveChanges();
                    }
                    catch (Exception)
                    {
                        RollBack();
                        throw;
                    }
                });
        }

        public void SetConnectionString(string connectionString)
        {
            if (_lazyConnection)
            {
                _connectionString = connectionString;
                InitializeConnection();
            }
        }

        public void SetIdentityCommand()
        {
            var container =
                ((IObjectContextAdapter)_context).ObjectContext.MetadataWorkspace.GetEntityContainer(
                    ((IObjectContextAdapter)_context).ObjectContext.DefaultContainerName,
                    DataSpace.CSpace);

            var sets = container.BaseEntitySets.ToList();

            foreach (var set in sets)
            {
                var command = string.Format("SET IDENTITY_INSERT {0} {1}", set.Name, "ON");
                ((IObjectContextAdapter)_context).ObjectContext.ExecuteStoreCommand(command);
            }
        }

        public void SetIsolationLevel(IsolationLevel isolationLevel)
        {
            _isolationLevel = isolationLevel;
        }

        public void SetTransactionType(TransactionTypes transactionType)
        {
            _transactionType = transactionType;
        }

        public void SetUseTransaction(bool useTransaction)
        {
            _useTransaction = useTransaction;
        }

        public IQueryable<TR> ExecuteSQLCommand<TR>(string command, params object[] parameters)
        {
            var result = Enumerable.Empty<TR>().AsQueryable();
            ProcessTransactionableMethod(
                () =>
                {
                    try
                    {
                        result = _context.Database.SqlQuery<TR>(command, parameters).AsQueryable();
                    }
                    catch (Exception error)
                    {
                        //error.FullTrace();
                        throw;
                    }
                });

            return result;
        }

        internal IQueryable<TR> ApplyIncludesToQuery<TR>(
            IQueryable<TR> entities,
            Expression<Func<TR, object>>[] includes) where TR : class
        {
            if (includes != null)
            {
                entities = includes.Aggregate(entities, (current, include) => current.Include(include));
            }

            return entities;
        }

        internal IQueryable<T> GetQuery(Expression<Func<T, object>> include)
        {
            var entities = SetEntities<T>().Include(include);

            return entities;
        }

        internal void InitializeConnection()
        {
            if (_context != null)
            {
                if (!string.IsNullOrEmpty(_connectionString))
                {
                    _context.Database.Connection.ConnectionString = _connectionString;
                }

                _connection = ((IObjectContextAdapter)_context).ObjectContext.Connection;
                _connection.Open();
            }
        }

        internal void InitializeRepository()
        {
            if (_context == null)
            {
                var instance = CreateContextInstance();

                SetUnchangedEntity(instance);
                _context = instance;

                if (_lazyConnection == false)
                {
                    InitializeConnection();
                }

                _context.Configuration.ProxyCreationEnabled = ProxyCreationEnabled;
            }
            else
            {
                _context.Configuration.LazyLoadingEnabled = false;
            }
        }

        internal void ProcessTransactionableMethod(Action action)
        {
            StartTransaction();
            action();
        }

        public void RollBack()
        {
            if (_useTransaction)
            {
                if (_transactionType == TransactionTypes.DbTransaction)
                {
                    if (_transaction != null && _transaction.Connection != null)
                    {
                        _transaction.Rollback();
                    }
                }
            }
        }

        internal IQueryable<TR> SetEntities<TR>() where TR : class
        {
            var entities = _context.Set<TR>();

            return entities;
        }

        internal DbSet<TR> SetEntity<TR>() where TR : class
        {
            var entity = _context.Set<TR>();

            return entity;
        }

        internal DbEntityEntry SetEntry<TR>(TR entity) where TR : class
        {
            var entry = _context.Entry(entity);

            return entry;
        }

        internal void StartTransaction()
        {
            if (_useTransaction)
            {
                switch (_transactionType)
                {
                    case TransactionTypes.DbTransaction:
                        {
                            if (_transaction == null || _transaction.Connection == null)
                            {
                                _transaction = _connection.BeginTransaction(_isolationLevel);
                            }

                            break;
                        }

                    case TransactionTypes.TransactionScope:
                        {
                            _transactionScope = new TransactionScope();
                            break;
                        }
                }
            }
        }

        private static bool IsProxy(object type)
        {
            return type != null && ObjectContext.GetObjectType(type.GetType()) != type.GetType();
        }

        private DbContext CreateContextInstance()
        {
            var instance = !string.IsNullOrEmpty(_connectionString)
                ? (DbContext)Activator.CreateInstance(typeof(T), _connectionString)
                : (DbContext)Activator.CreateInstance(typeof(T));
            return instance;
        }

        private void DisposeContext(ref DbContext context)
        {
            Trace.TraceWarning("Disposing {0}: {1}", typeof(T).Name, _index);
            //// Check if this can be deleted safely
            if (context != null)
            {
                if (context.Database.Connection != null && context.Database.Connection.State != ConnectionState.Closed)
                {
                    context.Database.Connection.Close();
                    context.Database.Connection.Dispose();
                }

                context.Dispose();
                context = null;
            }
        }

        private void SetUnchangedEntity(DbContext instance)
        {
            ((IObjectContextAdapter)instance).ObjectContext.ObjectMaterialized += (sender, args) =>
            {
                var entity = args.Entity as IObjectWithState;
                if (entity != null)
                {
                    entity.EntityState = State.Unchanged;
                }
            };
        }

        private void DetachAllUnchangedEntities()
        {
            var objectStateEntries =
                            _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Unchanged);
            foreach (var objectStateEntry in objectStateEntries)
            {
                objectStateEntry.State = EntityState.Detached;
            }
        }

        private void SetUnchangedState()
        {
            var modifiedOrAddedEntities = _context.ChangeTracker.Entries()
                     .Where(x => x.State == EntityState.Unchanged)
                     .ToList();

            foreach (var modifiedOrAddedEntity in modifiedOrAddedEntities)
            {
                ((IObjectWithState)(modifiedOrAddedEntity.Entity)).EntityState = State.Unchanged;
            }
        }

        private void SetStateForEntities()
        {
            var trackingEntries = _context.ChangeTracker.Entries<IObjectWithState>().ToList();
            foreach (var entry in trackingEntries)
            {
                var stateInfo = entry.Entity;
                entry.State = ConvertState(stateInfo.EntityState);
            }
        }
    }
}
