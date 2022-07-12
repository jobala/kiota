﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Models;

namespace Kiota.Builder.Extensions {
    public static class OpenApiSchemaExtensions {
        private static readonly Func<OpenApiSchema, IList<OpenApiSchema>> classNamesFlattener = (x) =>
        (x.AnyOf ?? Enumerable.Empty<OpenApiSchema>()).Union(x.AllOf).Union(x.OneOf).ToList();
        public static IEnumerable<string> GetSchemaNames(this OpenApiSchema schema) {
            if(schema == null)
                return Enumerable.Empty<string>();
            else if(schema.Items != null)
                return schema.Items.GetSchemaNames();
            else if(!string.IsNullOrEmpty(schema.Reference?.Id))
                return new string[] {schema.Reference.Id.Split('/').Last().Split('.').Last()};
            else if(schema.AnyOf.Any())
                return schema.AnyOf.FlattenIfRequired(classNamesFlattener);
            else if(schema.AllOf.Any())
                return schema.AllOf.FlattenIfRequired(classNamesFlattener);
            else if(schema.OneOf.Any())
                return schema.OneOf.FlattenIfRequired(classNamesFlattener);
            else if(!string.IsNullOrEmpty(schema.Title))
                return new string[] { schema.Title };
            else if(!string.IsNullOrEmpty(schema.Xml?.Name))
                return new string[] {schema.Xml.Name};
            else return Enumerable.Empty<string>();
        }
        private static IEnumerable<string> FlattenIfRequired(this IList<OpenApiSchema> schemas, Func<OpenApiSchema, IList<OpenApiSchema>> subsequentGetter) {
            var resultSet = schemas;
            if(schemas.Count == 1 && string.IsNullOrEmpty(schemas.First().Title))
                resultSet = schemas.FlattenEmptyEntries(subsequentGetter, 1);
            
            return resultSet.Select(x => x.Title).Where(x => !string.IsNullOrEmpty(x));
        }

        public static string GetSchemaName(this OpenApiSchema schema) {
            return schema.GetSchemaNames().LastOrDefault()?.TrimStart('$');// OData $ref
        }

        public static bool IsReferencedSchema(this OpenApiSchema schema) {
            var isReference = schema?.Reference != null;
            if(isReference && schema.Reference.IsExternal)
                throw new NotSupportedException("External references are not supported in this version of Kiota. While Kiota awaits on OpenAPI.Net to support inlining external references, you can use https://www.nuget.org/packages/Microsoft.OpenApi.Hidi to generate an OpenAPI description with inlined external references and then use this new reference with Kiota.");
            return isReference;
        }

        public static bool IsArray(this OpenApiSchema schema)
        {
            return (schema?.Type?.Equals("array", StringComparison.OrdinalIgnoreCase) ?? false) && schema?.Items != null;
        }

        public static bool IsObject(this OpenApiSchema schema)
        {
            return schema?.Type?.Equals("object", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        public static bool IsAnyOf(this OpenApiSchema schema)
        {
            return schema?.AnyOf?.Count > 1;
        }

        public static bool IsAllOf(this OpenApiSchema schema)
        {
            return schema?.AllOf?.Count > 1;
        }

        public static bool IsOneOf(this OpenApiSchema schema)
        {
            return schema?.OneOf?.Count > 1;
        }

        public static IEnumerable<string> GetSchemaReferenceIds(this OpenApiSchema schema, HashSet<OpenApiSchema> visitedSchemas = null) {
            visitedSchemas ??= new();            
            if(schema != null && !visitedSchemas.Contains(schema)) {
                visitedSchemas.Add(schema);
                var result = new List<string>();
                if(!string.IsNullOrEmpty(schema.Reference?.Id))
                    result.Add(schema.Reference.Id);
                if(schema.Items != null) {
                    if(!string.IsNullOrEmpty(schema.Items.Reference?.Id))
                        result.Add(schema.Items.Reference.Id);
                    result.AddRange(schema.Items.GetSchemaReferenceIds(visitedSchemas));
                }
                var subSchemaReferences = (schema.Properties?.Values ?? Enumerable.Empty<OpenApiSchema>())
                                            .Union(schema.AnyOf ?? Enumerable.Empty<OpenApiSchema>())
                                            .Union(schema.AllOf ?? Enumerable.Empty<OpenApiSchema>())
                                            .Union(schema.OneOf ?? Enumerable.Empty<OpenApiSchema>())
                                            .SelectMany(x => x.GetSchemaReferenceIds(visitedSchemas))
                                            .ToList();// this to list is important otherwise the any marks the schemas as visited and add range doesn't find anything
                if(subSchemaReferences.Any())
                    result.AddRange(subSchemaReferences);
                return result.Distinct();
            } else 
                return Enumerable.Empty<string>();
        }
        internal static IList<OpenApiSchema> FlattenEmptyEntries(this IList<OpenApiSchema> schemas, Func<OpenApiSchema, IList<OpenApiSchema>> subsequentGetter, int? maxDepth = default) {
            if(schemas == null) return default;
            if(subsequentGetter == null) throw new ArgumentNullException(nameof(subsequentGetter));

            if((maxDepth ?? 1) <= 0)
                return schemas;

            var result = schemas.ToList();
            var permutations = new Dictionary<OpenApiSchema, IList<OpenApiSchema>>();
            foreach(var item in result)
            {
                var subsequentItems = subsequentGetter(item);
                if(string.IsNullOrEmpty(item.Title) && subsequentItems.Any())
                    permutations.Add(item, subsequentItems.FlattenEmptyEntries(subsequentGetter, maxDepth.HasValue ? --maxDepth : default));
            }
            foreach(var permutation in permutations) {
                var index = result.IndexOf(permutation.Key);
                result.RemoveAt(index);
                var offset = 0;
                foreach(var insertee in permutation.Value) {
                    result.Insert(index + offset, insertee);
                    offset++;
                }
            }
            return result;
        }
    }
}
