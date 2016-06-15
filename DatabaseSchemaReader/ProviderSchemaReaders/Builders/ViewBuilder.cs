﻿using DatabaseSchemaReader.DataSchema;
using DatabaseSchemaReader.Filters;
using DatabaseSchemaReader.ProviderSchemaReaders.Adapters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseSchemaReader.ProviderSchemaReaders.Builders
{
    class ViewBuilder
    {
        private readonly ReaderAdapter _readerAdapter;
        private readonly Exclusions _exclusions;

        public event EventHandler<ReaderEventArgs> ReaderProgress;

        public ViewBuilder(ReaderAdapter readerAdapter, Exclusions exclusions)
        {
            _readerAdapter = readerAdapter;
            _exclusions = exclusions;
        }

        protected void RaiseReadingProgress(SchemaObjectType schemaObjectType)
        {
            ReaderEventArgs.RaiseEvent(ReaderProgress, this, ProgressType.ReadingSchema, schemaObjectType);
        }

        protected void RaiseProgress(ProgressType progressType,
            SchemaObjectType schemaObjectType,
            string name, int? index, int? count)
        {
            ReaderEventArgs.RaiseEvent(ReaderProgress, this, progressType, schemaObjectType,
    name, index, count);
        }

        public IList<DatabaseView> Execute(CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return new List<DatabaseView>();
            RaiseReadingProgress(SchemaObjectType.Views);
            var views = _readerAdapter.Views(null);

            if (ct.IsCancellationRequested) return views;
            ReaderEventArgs.RaiseEvent(ReaderProgress, this, ProgressType.Processing, SchemaObjectType.Views);
            var viewFilter = _exclusions.ViewFilter;
            if (viewFilter != null)
            {
                views = views.Where(t => !viewFilter.Exclude(t.Name)).ToList();
            }

            if (ct.IsCancellationRequested) return views;
            var sources = _readerAdapter.ViewSources(null);
            if (sources.Count > 0)
            {
                foreach (var view in views)
                {
                    var owner = view.SchemaOwner;
                    var name = view.Name;
                    var src = sources.FirstOrDefault(x => x.Name == name && x.SchemaOwner == owner);
                    if (src != null) view.Sql = src.Text;
                }
            }

            //get full datatables for all tables, to minimize database calls
            if (ct.IsCancellationRequested) return views;
            RaiseReadingProgress(SchemaObjectType.ViewColumns);

            var viewColumns = _readerAdapter.ViewColumns(null);
            var count = views.Count;
            for (var index = 0; index < count; index++)
            {
                if (ct.IsCancellationRequested) return views;
                DatabaseView v = views[index];
                ReaderEventArgs.RaiseEvent(ReaderProgress, this, ProgressType.Processing, SchemaObjectType.ViewColumns, v.Name, index, count);
                IEnumerable<DatabaseColumn> cols;
                if (viewColumns.Count == 0)
                {
                    cols = _readerAdapter.ViewColumns(v.Name);
                }
                else
                {
                    cols = viewColumns.Where(x => x.TableName == v.Name && x.SchemaOwner == v.SchemaOwner);
                }
                v.Columns.AddRange(cols);
            }
            return views;
        }
    }
}