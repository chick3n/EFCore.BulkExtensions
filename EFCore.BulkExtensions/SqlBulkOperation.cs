﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using FastMember;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace EFCore.BulkExtensions
{
    public enum OperationType
    {
        Insert,
        InsertOrUpdate,
        Update,
        Delete,
    }

    internal static class SqlBulkOperation
    {
        public static void Insert<T>(DbContext context, IList<T> entities, TableInfo tableInfo, Action<double> progress = null, int batchSize = 2000)
        {
            var sqlBulkCopy = new SqlBulkCopy(context.Database.GetDbConnection().ConnectionString)
            {
                DestinationTableName = tableInfo.InsertToTempTable ? tableInfo.FullTempTableName : tableInfo.FullTableName,
                BatchSize = batchSize,
                NotifyAfter = batchSize
            };
            sqlBulkCopy.SqlRowsCopied += (sender, e) => { progress?.Invoke(e.RowsCopied / entities.Count); };

            foreach (var element in tableInfo.PropertyColumnNamesDict)
            {
                sqlBulkCopy.ColumnMappings.Add(element.Key, element.Value);
            }
            using (var reader = ObjectReader.Create(entities, tableInfo.PropertyColumnNamesDict.Keys.ToArray()))
            {
                sqlBulkCopy.WriteToServer(reader);
            }
        }

        public static async Task InsertAsync<T>(DbContext context, IList<T> entities, TableInfo tableInfo, Action<double> progress = null, int batchSize = 2000)
        {
            var sqlBulkCopy = new SqlBulkCopy(context.Database.GetDbConnection().ConnectionString)
            {
                DestinationTableName = tableInfo.InsertToTempTable ? tableInfo.FullTempTableName : tableInfo.FullTableName,
                BatchSize = batchSize,
                NotifyAfter = batchSize
            };
            sqlBulkCopy.SqlRowsCopied += (sender, e) => { progress?.Invoke(e.RowsCopied / entities.Count); };

            foreach (var element in tableInfo.PropertyColumnNamesDict)
            {
                sqlBulkCopy.ColumnMappings.Add(element.Key, element.Value);
            }
            using (var reader = ObjectReader.Create(entities, tableInfo.PropertyColumnNamesDict.Keys.ToArray()))
            {
                await sqlBulkCopy.WriteToServerAsync(reader);
            }
        }

        public static void Merge<T>(DbContext context, IList<T> entities, TableInfo tableInfo, OperationType operationType) where T : class
        {
            tableInfo.InsertToTempTable = true;
            tableInfo.CheckHasIdentity(context);

            context.Database.ExecuteSqlCommand(SqlQueryBuilder.CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName));
            if (tableInfo.BulkConfig.SetOutputIdentity)
            {
                context.Database.ExecuteSqlCommand(SqlQueryBuilder.CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempOutputTableName));
            }
            try
            {
                SqlBulkOperation.Insert<T>(context, entities, tableInfo);
                context.Database.ExecuteSqlCommand(SqlQueryBuilder.MergeTable(tableInfo, operationType));
                context.Database.ExecuteSqlCommand(SqlQueryBuilder.DropTable(tableInfo.FullTempTableName));

                if (tableInfo.BulkConfig.SetOutputIdentity)
                {
                    var entitiesWithOutputIdentity = context.Set<T>().FromSql(SqlQueryBuilder.SelectFromTable(tableInfo.FullTempOutputTableName, tableInfo.PrimaryKeyFormated)).ToList();
                    if (tableInfo.BulkConfig.PreserveInsertOrder) // Updates PK in entityList
                    {
                        Type type = typeof(T);
                        var accessor = TypeAccessor.Create(type);
                        for (int i = 0; i < tableInfo.NumberOfEntities; i++)
                            accessor[entities[i], tableInfo.PrimaryKey] = accessor[entitiesWithOutputIdentity[i], tableInfo.PrimaryKey];
                    }
                    else // Clears entityList and then refill it with loaded entites from Db
                    {
                        entities.Clear();
                        ((List<T>)entities).AddRange(entitiesWithOutputIdentity);
                    }
                    context.Database.ExecuteSqlCommand(SqlQueryBuilder.DropTable(tableInfo.FullTempOutputTableName));
                }
            }
            catch (Exception ex)
            {
                if (tableInfo.BulkConfig.SetOutputIdentity)
                {
                    context.Database.ExecuteSqlCommand(SqlQueryBuilder.DropTable(tableInfo.FullTempOutputTableName));
                }
                context.Database.ExecuteSqlCommand(SqlQueryBuilder.DropTable(tableInfo.FullTempTableName));
                throw ex;
            }
        }

        public static async Task MergeAsync<T>(DbContext context, IList<T> entities, TableInfo tableInfo, OperationType operationType) where T : class
        {
            tableInfo.InsertToTempTable = true;
            await tableInfo.CheckHasIdentityAsync(context);

            await context.Database.ExecuteSqlCommandAsync(SqlQueryBuilder.CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName));
            if (tableInfo.BulkConfig.SetOutputIdentity)
            {
                await context.Database.ExecuteSqlCommandAsync(SqlQueryBuilder.CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempOutputTableName));
            }
            try
            {
                await SqlBulkOperation.InsertAsync<T>(context, entities, tableInfo);
                await context.Database.ExecuteSqlCommandAsync(SqlQueryBuilder.MergeTable(tableInfo, operationType));
                await context.Database.ExecuteSqlCommandAsync(SqlQueryBuilder.DropTable(tableInfo.FullTempTableName));

                if (tableInfo.BulkConfig.SetOutputIdentity)
                {
                    var entitiesWithOutputIdentity = context.Set<T>().FromSql(SqlQueryBuilder.SelectFromTable(tableInfo.FullTempOutputTableName, tableInfo.PrimaryKeyFormated)).ToList();
                    if (tableInfo.BulkConfig.PreserveInsertOrder) // Updates PK in entityList
                    {
                        Type type = typeof(T);
                        var accessor = TypeAccessor.Create(type);
                        for (int i = 0; i < tableInfo.NumberOfEntities; i++)
                            accessor[entities[i], tableInfo.PrimaryKey] = accessor[entitiesWithOutputIdentity[i], tableInfo.PrimaryKey];
                    }
                    else // Clears entityList and then refill it with loaded entites from Db
                    {
                        entities.Clear();
                        ((List<T>)entities).AddRange(entitiesWithOutputIdentity);
                    }
                    await context.Database.ExecuteSqlCommandAsync(SqlQueryBuilder.DropTable(tableInfo.FullTempOutputTableName));
                }
            }
            catch (Exception ex)
            {
                if (tableInfo.BulkConfig.SetOutputIdentity)
                {
                    await context.Database.ExecuteSqlCommandAsync(SqlQueryBuilder.DropTable(tableInfo.FullTempOutputTableName));
                }
                await context.Database.ExecuteSqlCommandAsync(SqlQueryBuilder.DropTable(tableInfo.FullTempTableName));
                throw ex;
            }
        }
    }
}
