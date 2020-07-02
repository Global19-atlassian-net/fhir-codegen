﻿// <copyright file="LanguageInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Health.Fhir.SpecManager.Manager;
using Microsoft.Health.Fhir.SpecManager.Models;

namespace Microsoft.Health.Fhir.SpecManager.Language
{
    /// <summary>Export to an Information format - used to check parsing and dump FHIR version info.</summary>
    public sealed class LanguageInfo : ILanguage
    {
        /// <summary>FHIR information we are exporting.</summary>
        private FhirVersionInfo _info;

        /// <summary>Options for controlling the export.</summary>
        private ExporterOptions _options;

        /// <summary>The currently in-use text writer.</summary>
        private ExportStreamWriter _writer;

        /// <summary>Name of the language.</summary>
        private const string _languageName = "Info";

        /// <summary>Dictionary mapping FHIR primitive types to language equivalents.</summary>
        private static readonly Dictionary<string, string> _primitiveTypeMap = new Dictionary<string, string>()
        {
            { "base", "base" },
            { "base64Binary", "base64Binary" },
            { "boolean", "boolean" },
            { "canonical", "canonical" },
            { "code", "code" },
            { "date", "date" },
            { "dateTime", "dateTime" },
            { "decimal", "decimal" },
            { "id", "id" },
            { "instant", "instant" },
            { "integer", "integer" },
            { "integer64", "integer64" },
            { "markdown", "markdown" },
            { "oid", "oid" },
            { "positiveInt", "positiveInt" },
            { "string", "string" },
            { "time", "time" },
            { "unsignedInt", "unsignedInt" },
            { "uri", "uri" },
            { "url", "url" },
            { "uuid", "uuid" },
            { "xhtml", "xhtml" },
        };

        /// <summary>Gets the reserved words.</summary>
        /// <value>The reserved words.</value>
        private static readonly HashSet<string> _reservedWords = new HashSet<string>();

        /// <summary>Gets the name of the language.</summary>
        /// <value>The name of the language.</value>
        string ILanguage.LanguageName => _languageName;

        /// <summary>Gets a value indicating whether the language supports model inheritance.</summary>
        /// <value>True if the language supports model inheritance, false if not.</value>
        bool ILanguage.SupportsModelInheritance => true;

        /// <summary>Gets a value indicating whether the supports hiding parent field.</summary>
        /// <value>True if the language supports hiding parent field, false if not.</value>
        bool ILanguage.SupportsHidingParentField => true;

        /// <summary>
        /// Gets a value indicating whether the language supports nested type definitions.
        /// </summary>
        /// <value>True if the language supports nested type definitions, false if not.</value>
        bool ILanguage.SupportsNestedTypeDefinitions => true;

        /// <summary>Gets a value indicating whether the supports slicing.</summary>
        /// <value>True if supports slicing, false if not.</value>
        bool ILanguage.SupportsSlicing => true;

        /// <summary>Gets the FHIR primitive type map.</summary>
        /// <value>The FHIR primitive type map.</value>
        Dictionary<string, string> ILanguage.FhirPrimitiveTypeMap => _primitiveTypeMap;

        /// <summary>Gets the reserved words.</summary>
        /// <value>The reserved words.</value>
        HashSet<string> ILanguage.ReservedWords => _reservedWords;

        /// <summary>
        /// Gets a list of FHIR class types that the language WILL export, regardless of user choices.
        /// Used to provide information to users.
        /// </summary>
        List<ExporterOptions.FhirExportClassType> ILanguage.RequiredExportClassTypes => new List<ExporterOptions.FhirExportClassType>()
        {
            ExporterOptions.FhirExportClassType.PrimitiveType,
            ExporterOptions.FhirExportClassType.ComplexType,
            ExporterOptions.FhirExportClassType.Resource,
            ExporterOptions.FhirExportClassType.Interaction,
            ExporterOptions.FhirExportClassType.Enum,
        };

        /// <summary>
        /// Gets a list of FHIR class types that the language CAN export, depending on user choices.
        /// </summary>
        List<ExporterOptions.FhirExportClassType> ILanguage.OptionalExportClassTypes => new List<ExporterOptions.FhirExportClassType>();

        /// <summary>Gets language-specific options and their descriptions.</summary>
        Dictionary<string, string> ILanguage.LanguageOptions => new Dictionary<string, string>();

        /// <summary>Export the passed FHIR version into the specified directory.</summary>
        /// <param name="info">           The information.</param>
        /// <param name="options">        Options for controlling the operation.</param>
        /// <param name="exportDirectory">Directory to write files.</param>
        void ILanguage.Export(
            FhirVersionInfo info,
            ExporterOptions options,
            string exportDirectory)
        {
            // set internal vars so we don't pass them to every function
            // this is ugly, but the interface patterns get bad quickly because we need the type map to copy the FHIR info
            _info = info;
            _options = options;

            // create a filename for writing (single file for now)
            string filename = Path.Combine(exportDirectory, $"R{info.MajorVersion}.txt");

            using (FileStream stream = new FileStream(filename, FileMode.Create))
            using (ExportStreamWriter writer = new ExportStreamWriter(stream))
            {
                _writer = writer;

                WriteHeader();

                WritePrimitiveTypes(_info.PrimitiveTypes.Values);
                WriteComplexes(_info.ComplexTypes.Values, "Complex Types");
                WriteComplexes(_info.Resources.Values, "Resources");

                WriteOperations(_info.SystemOperations.Values, true, "System Operations");
                WriteSearchParameters(_info.AllResourceParameters.Values, "All Resource Parameters");
                WriteSearchParameters(_info.SearchResultParameters.Values, "Search Result Parameters");
                WriteSearchParameters(_info.AllInteractionParameters.Values, "All Interaction Parameters");

                WriteValueSets(_info.ValueSetsByUrl.Values, "Value Sets");

                WriteFooter();
            }
        }

        /// <summary>Writes a value sets.</summary>
        /// <param name="valueSets"> Sets the value belongs to.</param>
        /// <param name="headerHint">(Optional) The header hint.</param>
        private void WriteValueSets(
            IEnumerable<FhirValueSetCollection> valueSets,
            string headerHint = null)
        {
            if (!string.IsNullOrEmpty(headerHint))
            {
                _writer.WriteLineI($"{headerHint}: {valueSets.Count()} (unversioned)");
            }

            foreach (FhirValueSetCollection collection in valueSets.OrderBy(c => c.URL))
            {
                foreach (FhirValueSet vs in collection.ValueSetsByVersion.Values.OrderBy(v => v.Version))
                {
                    _writer.WriteLineI($"- ValueSet: {vs.URL}|{vs.Version}");

                    _writer.IncreaseIndent();

                    foreach (FhirConcept value in vs.Concepts)
                    {
                        _writer.WriteLineI($"- #{value.Code}: {value.Display}");
                    }

                    _writer.DecreaseIndent();
                }
            }
        }

        /// <summary>Writes a value set.</summary>
        /// <param name="valueSet">Set the value belongs to.</param>
        private void WriteValueSet(
            FhirValueSet valueSet)
        {
            _writer.WriteLineI($"- {valueSet.URL}|{valueSet.Version} ({valueSet.Name})");

            _writer.IncreaseIndent();

            foreach (FhirConcept concept in valueSet.Concepts.OrderBy(c => c.Code))
            {
                _writer.WriteLineI($"- #{concept.Code}: {concept.Display}");
            }

            _writer.DecreaseIndent();
        }

        /// <summary>Writes the complexes.</summary>
        /// <param name="complexes"> The complexes.</param>
        /// <param name="headerHint">(Optional) The header hint.</param>
        private void WriteComplexes(
            IEnumerable<FhirComplex> complexes,
            string headerHint = null)
        {
            if (!string.IsNullOrEmpty(headerHint))
            {
                _writer.WriteLineI($"{headerHint}: {complexes.Count()}");
            }

            foreach (FhirComplex complex in complexes)
            {
                WriteComplex(complex);
            }
        }

        /// <summary>Writes a primitive types.</summary>
        /// <param name="primitives">The primitives.</param>
        private void WritePrimitiveTypes(
            IEnumerable<FhirPrimitive> primitives)
        {
            _writer.WriteLineI( $"Primitive Types: {primitives.Count()}");

            foreach (FhirPrimitive primitive in primitives)
            {
                WritePrimitiveType(primitive);
            }
        }

        /// <summary>Writes a primitive type.</summary>
        /// <param name="primitive">The primitive.</param>
        private void WritePrimitiveType(
            FhirPrimitive primitive)
        {
            _writer.WriteLineI(
                $"- {primitive.Name}:" +
                    $" {primitive.NameForExport(FhirTypeBase.NamingConvention.CamelCase)}" +
                    $"::{primitive.TypeForExport(FhirTypeBase.NamingConvention.CamelCase, _primitiveTypeMap)}");

            _writer.IncreaseIndent();

            // check for regex
            if (!string.IsNullOrEmpty(primitive.ValidationRegEx))
            {
                _writer.WriteLineI($"[{primitive.ValidationRegEx}]");
            }

            if (_info.ExtensionsByPath.ContainsKey(primitive.Path))
            {
                WriteExtensions(_info.ExtensionsByPath[primitive.Name].Values);
            }

            _writer.DecreaseIndent();
        }

        /// <summary>Writes the extensions.</summary>
        /// <param name="extensions">The extensions.</param>
        private void WriteExtensions(
            IEnumerable<FhirComplex> extensions)
        {
            _writer.WriteLineI($"Extensions: {extensions.Count()}");

            foreach (FhirComplex extension in extensions)
            {
                WriteExtension(extension);
            }
        }

        /// <summary>Writes an extension.</summary>
        /// <param name="extension">The extension.</param>
        private void WriteExtension(
            FhirComplex extension)
        {
            _writer.WriteLineI($"+{extension.URL}");

            if (extension.Elements.Count > 0)
            {
                WriteComplex(extension);
            }
        }

        /// <summary>Writes a complex.</summary>
        /// <param name="complex">The complex.</param>
        private void WriteComplex(
            FhirComplex complex)
        {
            bool indented = false;

            // write this type's line, if it's a root element
            // (sub-properties are written with cardinality in the prior loop)
            if (_writer.Indentation == 0)
            {
                _writer.WriteLine($"- {complex.Name}: {complex.BaseTypeName}");
                _writer.IncreaseIndent();
                indented = true;
            }

            // write elements
            WriteElements(complex);

            // check for extensions
            if (_info.ExtensionsByPath.ContainsKey(complex.Path))
            {
                WriteExtensions(_info.ExtensionsByPath[complex.Path].Values);
            }

            // check for search parameters on this object
            if (complex.SearchParameters != null)
            {
                WriteSearchParameters(complex.SearchParameters.Values);
            }

            // check for type operations
            if (complex.TypeOperations != null)
            {
                WriteOperations(complex.TypeOperations.Values, true);
            }

            // check for instance operations
            if (complex.InstanceOperations != null)
            {
                WriteOperations(complex.TypeOperations.Values, false);
            }

            if (indented)
            {
                _writer.DecreaseIndent();
            }
        }

        /// <summary>Writes the operations.</summary>
        /// <param name="operations"> The operations.</param>
        /// <param name="isTypeLevel">True if is type level, false if not.</param>
        /// <param name="headerHint"> (Optional) The header hint.</param>
        private void WriteOperations(
            IEnumerable<FhirOperation> operations,
            bool isTypeLevel,
            string headerHint = null)
        {
            bool indented = false;

            if (!string.IsNullOrEmpty(headerHint))
            {
                _writer.WriteLineI($"{headerHint}: {operations.Count()}");
                _writer.IncreaseIndent();
                indented = true;
            }

            foreach (FhirOperation operation in operations)
            {
                if (isTypeLevel)
                {
                    _writer.WriteLineI($"${operation.Code}");
                }
                else
                {
                    _writer.WriteLineI($"/{{id}}${operation.Code}");
                }

                if (operation.Parameters != null)
                {
                    _writer.IncreaseIndent();

                    // write operation parameters inline
                    foreach (FhirParameter parameter in operation.Parameters.OrderBy(p => p.FieldOrder))
                    {
                        _writer.WriteLineI($"{parameter.Use}: {parameter.Name} ({parameter.FhirCardinality})");
                    }

                    _writer.DecreaseIndent();
                }
            }

            if (indented)
            {
                _writer.DecreaseIndent();
            }
        }

        /// <summary>Writes search parameters.</summary>
        /// <param name="searchParameters">Options for controlling the search.</param>
        /// <param name="headerHint">      (Optional) The header hint.</param>
        private void WriteSearchParameters(
            IEnumerable<FhirSearchParam> searchParameters,
            string headerHint = null)
        {
            bool indented = false;

            if (!string.IsNullOrEmpty(headerHint))
            {
                _writer.WriteLineI($"{headerHint}: {searchParameters.Count()}");
                _writer.IncreaseIndent();
                indented = true;
            }

            foreach (FhirSearchParam searchParam in searchParameters)
            {
                _writer.WriteLineI($"?{searchParam.Code}={searchParam.ValueType} ({searchParam.Name})");
            }

            if (indented)
            {
                _writer.DecreaseIndent();
            }
        }

        /// <summary>Writes the elements.</summary>
        /// <param name="complex">    The complex.</param>
        private void WriteElements(
            FhirComplex complex)
        {
            foreach (FhirElement element in complex.Elements.Values.OrderBy(s => s.FieldOrder))
            {
                WriteElement(complex, element);
            }
        }

        /// <summary>Writes an element.</summary>
        /// <param name="complex">The complex.</param>
        /// <param name="element">The element.</param>
        private void WriteElement(
            FhirComplex complex,
            FhirElement element)
        {
            string propertyType = string.Empty;

            if (element.ElementTypes != null)
            {
                foreach (FhirElementType elementType in element.ElementTypes.Values)
                {
                    string joiner = string.IsNullOrEmpty(propertyType) ? string.Empty : "|";

                    string profiles = string.Empty;
                    if ((elementType.Profiles != null) && (elementType.Profiles.Count > 0))
                    {
                        profiles = "(" + string.Join("|", elementType.Profiles.Values) + ")";
                    }

                    propertyType = $"{propertyType}{joiner}{elementType.Name}{profiles}";
                }
            }

            if (string.IsNullOrEmpty(propertyType))
            {
                propertyType = element.BaseTypeName;
            }

            _writer.WriteLineI(
                $"-" +
                $" {element.NameForExport(FhirTypeBase.NamingConvention.CamelCase)}[{element.FhirCardinality}]:" +
                $" {propertyType}");

            _writer.IncreaseIndent();

            // check for regex
            if (!string.IsNullOrEmpty(element.ValidationRegEx))
            {
                _writer.WriteLineI($"[{element.ValidationRegEx}]");
            }

            // check for default value
            if (!string.IsNullOrEmpty(element.DefaultFieldName))
            {
                _writer.WriteLineI($".{element.DefaultFieldName} = {element.DefaultFieldValue}");
            }

            // check for fixed value
            if (!string.IsNullOrEmpty(element.FixedFieldName))
            {
                _writer.WriteLineI($".{element.FixedFieldName} = {element.FixedFieldValue}");
            }

            if ((element.Codes != null) && (element.Codes.Count > 0))
            {
                string codes = string.Join("|", element.Codes);
                _writer.WriteLineI( $"{{{codes}}}");
            }

            // either step into backbone definition OR extensions, don't write both
            if (complex.Components.ContainsKey(element.Path))
            {
                WriteComplex(complex.Components[element.Path]);
            }
            else if (_info.ExtensionsByPath.ContainsKey(element.Path))
            {
                WriteExtensions(_info.ExtensionsByPath[element.Path].Values);
            }

            // check for slicing information
            if (element.Slicing != null)
            {
                WriteSlicings(element.Slicing.Values);
            }

            _writer.DecreaseIndent();
        }

        /// <summary>Writes the slicings.</summary>
        /// <param name="slicings">The slicings.</param>
        private void WriteSlicings(
            IEnumerable<FhirSlicing> slicings)
        {
            foreach (FhirSlicing slicing in slicings)
            {
                if (slicing.Slices.Count == 0)
                {
                    continue;
                }

                WriteSlicing(slicing);
            }
        }

        /// <summary>Writes a slicing.</summary>
        /// <param name="slicing">The slicing.</param>
        private void WriteSlicing(
            FhirSlicing slicing)
        {
            string rules = string.Empty;

            foreach (FhirSliceDiscriminatorRule rule in slicing.DiscriminatorRules.Values)
            {
                if (!string.IsNullOrEmpty(rules))
                {
                    rules += ", ";
                }

                rules += $"{rule.DiscriminatorTypeName}@{rule.Path}";
            }

            _writer.WriteLineI($": {slicing.DefinedByUrl} - {slicing.SlicingRules} ({rules})");

            _writer.IncreaseIndent();

            // write slices inline
            int sliceNumber = 0;
            foreach (FhirComplex slice in slicing.Slices)
            {
                _writer.WriteLineI($": Slice {sliceNumber++}:{slice.SliceName} - on {slice.Name}");

                _writer.IncreaseIndent();

                // recurse into this slice
                WriteComplex(slice);

                _writer.DecreaseIndent();
            }

            _writer.DecreaseIndent();
        }

        /// <summary>Writes a header.</summary>
        private void WriteHeader()
        {
            _writer.WriteLine($"Contents of: {_info.PackageName} version: {_info.VersionString}");
            _writer.WriteLine($"  Using Model Inheritance: {_options.UseModelInheritance}");
            _writer.WriteLine($"  Hiding Removed Parent Fields: {_options.HideRemovedParentFields}");
            _writer.WriteLine($"  Nesting Type Definitions: {_options.NestTypeDefinitions}");
            _writer.WriteLine($"  Primitive Naming Style: {FhirTypeBase.NamingConvention.CamelCase}");
            _writer.WriteLine($"  Element Naming Style: {FhirTypeBase.NamingConvention.CamelCase}");
            _writer.WriteLine($"  Complex Type / Resource Naming Style: {FhirTypeBase.NamingConvention.PascalCase}");
            _writer.WriteLine($"  Enum Naming Style: {FhirTypeBase.NamingConvention.FhirDotNotation}");
            _writer.WriteLine($"  Interaction Naming Style: {FhirTypeBase.NamingConvention.PascalCase}");
            _writer.WriteLine($"  Extension Support: {_options.ExtensionSupport}");

            if ((_options.ExportList != null) && _options.ExportList.Any())
            {
                string restrictions = string.Join("|", _options.ExportList);
                _writer.WriteLine($"  Restricted to: {restrictions}");
            }

            if ((_options.LanguageOptions != null) && (_options.LanguageOptions.Count > 0))
            {
                foreach (KeyValuePair<string, string> kvp in _options.LanguageOptions)
                {
                    _writer.WriteLine($"  Language option: \"{kvp.Key}\" = \"{kvp.Value}\"");
                }
            }
        }

        /// <summary>Writes a footer.</summary>
        private void WriteFooter()
        {
            return;
        }
    }
}
