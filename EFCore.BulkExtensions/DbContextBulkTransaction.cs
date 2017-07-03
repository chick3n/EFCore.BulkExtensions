﻿using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace EFCore.BulkExtensions
{
    internal static class DbContextBulkTransaction
    {
        public static void Execute<T>(DbContext context, IList<T> entities, OperationType operationType, BulkConfig bulkConfig) where T : class
        {
            var tableInfo = new TableInfo();
            var isDeleteOperation = operationType == OperationType.Delete;
            tableInfo.NumberOfEntities = entities.Count;
            tableInfo.LoadData<T>(context, isDeleteOperation);
            tableInfo.BulkConfig = bulkConfig ?? new BulkConfig();

            if (operationType == OperationType.Insert && !tableInfo.BulkConfig.SetOutputIdentity)
            {
                SqlBulkOperation.Insert<T>(context, entities, tableInfo);
            }
            else
            {
                SqlBulkOperation.Merge<T>(context, entities, tableInfo, operationType);
            }
        }

        public static async Task ExecuteAsync<T>(DbContext context, IList<T> entities, OperationType operationType, BulkConfig bulkConfig) where T : class
        {
            var tableInfo = new TableInfo();
            var isDeleteOperation = operationType == OperationType.Delete;
            tableInfo.NumberOfEntities = entities.Count;
            tableInfo.LoadData<T>(context, isDeleteOperation);
            tableInfo.BulkConfig = bulkConfig ?? new BulkConfig();

            if (operationType == OperationType.Insert && !tableInfo.BulkConfig.SetOutputIdentity)
            {
                await SqlBulkOperation.InsertAsync<T>(context, entities, tableInfo);
            }
            else
            {
                await SqlBulkOperation.MergeAsync<T>(context, entities, tableInfo, operationType);
            }
        }
    }
}
