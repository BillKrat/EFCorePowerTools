using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using RevEng.Common;

namespace RevEng.Core
{
    public class ColumnRemovingScaffoldingModelFactory : RelationalScaffoldingModelFactory
    {
        private readonly List<SerializationTableModel> tables;
        private readonly DatabaseType databaseType;
        private readonly bool ignoreManyToMany;

        public ColumnRemovingScaffoldingModelFactory([NotNull] IOperationReporter reporter, [NotNull] ICandidateNamingService candidateNamingService, [NotNull] IPluralizer pluralizer, [NotNull] ICSharpUtilities cSharpUtilities, [NotNull] IScaffoldingTypeMapper scaffoldingTypeMapper, [NotNull] LoggingDefinitions loggingDefinitions, [NotNull] IModelRuntimeInitializer modelRuntimeInitializer, List<SerializationTableModel> tables, DatabaseType databaseType, bool ignoreManyToMany)
            : base(reporter, candidateNamingService, pluralizer, cSharpUtilities, scaffoldingTypeMapper, modelRuntimeInitializer)
        {
            this.tables = tables;
            this.databaseType = databaseType;
            this.ignoreManyToMany = ignoreManyToMany;
        }

        protected override EntityTypeBuilder VisitTable(ModelBuilder modelBuilder, DatabaseTable table)
        {
            ArgumentNullException.ThrowIfNull(table);

            string name;
            if (databaseType == DatabaseType.SQLServer || databaseType == DatabaseType.SQLServerDacpac)
            {
                name = $"[{table.Schema}].[{table.Name}]";
            }
            else
            {
                name = string.IsNullOrEmpty(table.Schema)
                    ? table.Name
                    : $"{table.Schema}.{table.Name}";
            }

            var excludedColumns = new List<DatabaseColumn>();
            var tableDefinition = tables.Find(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (tableDefinition?.ExcludedColumns != null)
            {
                foreach (var column in tableDefinition.ExcludedColumns!)
                {
                    var columnToRemove = table.Columns.FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
                    if (columnToRemove != null)
                    {
                        excludedColumns.Add(columnToRemove);
                        table.Columns.Remove(columnToRemove);
                    }
                }
            }

            if (excludedColumns.Count > 0)
            {
                var indexesToBeRemoved = new List<DatabaseIndex>();
                foreach (var index in table.Indexes)
                {
                    indexesToBeRemoved.AddRange(index.Columns.Where(column => excludedColumns.Contains(column)).Select(column => index));
                }

                foreach (var index in indexesToBeRemoved)
                {
                    table.Indexes.Remove(index);
                }

                var constraintsToBeRemoved = new List<DatabaseUniqueConstraint>();
                foreach (var constraint in table.UniqueConstraints)
                {
                    constraintsToBeRemoved.AddRange(constraint.Columns.Where(column => excludedColumns.Contains(column)).Select(column => constraint));
                }

                foreach (var constraint in constraintsToBeRemoved)
                {
                    table.UniqueConstraints.Remove(constraint);
                }

                var fksToBeRemoved = new List<DatabaseForeignKey>();
                foreach (var fk in table.ForeignKeys)
                {
                    fksToBeRemoved.AddRange(fk.Columns.Where(column => excludedColumns.Contains(column)).Select(column => fk));
                }

                foreach (var fk in fksToBeRemoved)
                {
                    table.ForeignKeys.Remove(fk);
                }
            }

            return base.VisitTable(modelBuilder, table);
        }

        protected override ModelBuilder VisitForeignKeys(ModelBuilder modelBuilder, IList<DatabaseForeignKey> foreignKeys)
        {
            ArgumentNullException.ThrowIfNull(foreignKeys);

            ArgumentNullException.ThrowIfNull(modelBuilder);

            if (ignoreManyToMany)
            {
                foreach (var fk in foreignKeys)
                {
                    VisitForeignKey(modelBuilder, fk);
                }

                foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                {
                    foreach (var foreignKey in entityType.GetForeignKeys())
                    {
                        AddNavigationProperties(foreignKey);
                    }
                }

                return modelBuilder;
            }

            return base.VisitForeignKeys(modelBuilder, foreignKeys);
        }
    }
}