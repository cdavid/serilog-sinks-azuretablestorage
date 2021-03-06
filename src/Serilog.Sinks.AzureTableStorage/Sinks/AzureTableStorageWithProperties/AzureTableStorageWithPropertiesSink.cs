﻿// Copyright 2014 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.AzureTableStorage.KeyGenerator;
using Serilog.Sinks.AzureTableStorage.Sinks.KeyGenerator;

namespace Serilog.Sinks.AzureTableStorage
{
    /// <summary>
    /// Writes log events as records to an Azure Table Storage table storing properties as columns.
    /// </summary>
    public class AzureTableStorageWithPropertiesSink : ILogEventSink
    {
        private readonly int _waitTimeoutMilliseconds = Timeout.Infinite;
        private readonly IFormatProvider _formatProvider;
        private readonly CloudTable _table;
        private readonly string _additionalRowKeyPostfix;
        private readonly string[] _propertyColumns;
        private readonly IKeyGenerator _keyGenerator;

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="storageAccount">The Cloud Storage Account to use to insert the log entries to.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="storageTableName">Table name that log entries will be written to. Note: Optional, setting this may impact performance</param>
        /// <param name="additionalRowKeyPostfix">Additional postfix string that will be appended to row keys</param>
        /// <param name="keyGenerator">Generates the PartitionKey and the RowKey</param>
        /// <param name="propertyColumns">Specific properties to be written to columns. By default, all properties will be written to columns.</param>
        /// <param name="bypassTableCreationValidation">Bypass the exception in case the table creation fails.</param>
        public AzureTableStorageWithPropertiesSink(CloudStorageAccount storageAccount,
            IFormatProvider formatProvider,
            string storageTableName = null,
            string additionalRowKeyPostfix = null,
            IKeyGenerator keyGenerator = null,
            string[] propertyColumns = null,
            bool bypassTableCreationValidation = false)
        {
            var tableClient = storageAccount.CreateCloudTableClient();

            if (string.IsNullOrEmpty(storageTableName))
            {
                storageTableName = "LogEventEntity";
            }

            _table = tableClient.GetTableReference(storageTableName);

            try
            {
                _table.CreateIfNotExistsAsync().SyncContextSafeWait(_waitTimeoutMilliseconds);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Failed to create table: {ex}");
                if (!bypassTableCreationValidation)
                {
                    throw;
                }
            }

            _formatProvider = formatProvider;
            _additionalRowKeyPostfix = additionalRowKeyPostfix;
            _propertyColumns = propertyColumns;
            _keyGenerator = keyGenerator ?? new PropertiesKeyGenerator();
        }

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        public void Emit(LogEvent logEvent)
        {
            var op = TableOperation.Insert(AzureTableStorageEntityFactory.CreateEntityWithProperties(logEvent, _formatProvider, _additionalRowKeyPostfix, _keyGenerator, _propertyColumns));

            _table.ExecuteAsync(op).SyncContextSafeWait(_waitTimeoutMilliseconds);
        }
    }
}
