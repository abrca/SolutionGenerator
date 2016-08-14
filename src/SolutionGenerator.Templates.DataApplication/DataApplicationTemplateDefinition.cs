﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DataApplicationTemplate.cs" company="WildGums">
//   Copyright (c) 2012 - 2016 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SolutionGenerator.Templates.DataApplication
{
	using System;
	using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
	using System.Linq;
	using System.Windows;
    using Catel;
	using Catel.IoC;
	using Catel.Logging;
	using Catel.Reflection;
	using Orc.Csv;
	using Services;
	using Views;

    public class DataApplicationTemplateDefinition : TemplateDefinitionBase<DataApplicationTemplateContext>
    {
		private static readonly ILog Log = LogManager.GetCurrentClassLogger();
		private DataApplicationTemplateContext _templateContext = null;


	    public override List<ITemplateFile> GetTemplateFiles()
        {
            var assemblyDirectory = GetType().Assembly.GetDirectory();
            var filesDirectory = Path.Combine(assemblyDirectory, "Files");

            var files = TemplateFileHelper.FindFiles(filesDirectory);
            return files;
        }

        public override FrameworkElement GetView()
        {
            return new SettingsView();
        }

	    public override void PostGenerate()
	    {
			ProcessDataFolder(TemplateContext.Solution.Directory);
			var gitService = ServiceLocator.Default.ResolveType<IGitService>();
			gitService.InitGitRepository(TemplateContext.Solution.Directory);
			base.PostGenerate();
	    }


		private void ProcessDataFolder(string root)
		{
			_templateContext = (DataApplicationTemplateContext) TemplateContext;

			var fileSystemService = ServiceLocator.Default.ResolveType<IFileSystemService>();
			var codeGenerationService = ServiceLocator.Default.ResolveType<ICodeGenerationService>();
			var projectFileService = ServiceLocator.Default.ResolveType<IProjectFileService>();
			var entityPluralService = ServiceLocator.Default.ResolveType<IEntityPluralService>();

			CreateModelFiles(root, fileSystemService, codeGenerationService, projectFileService);
			CreateDataFiles(root, fileSystemService, projectFileService);
			CreateSingleTestClassFile(root, fileSystemService, projectFileService, entityPluralService);
		}

		private void CreateModelFiles(string root, IFileSystemService fileSystemService, ICodeGenerationService codeGenerationService, IProjectFileService projectFileService)
		{
			var modelFolder = fileSystemService.Folders(root, "*.*").FirstOrDefault(f => f.ToLower().Contains("\\model"));
			if (modelFolder == null)
			{
				return;
			}

			var files = fileSystemService.Files(_templateContext.Data.DataFolder, "*.csv").ToArray();
			foreach (var file in files)
			{
				try
				{
					codeGenerationService.CreateCSharpFiles(Path.GetFullPath(file), TemplateContext.Solution.Name, modelFolder);
				}
				catch (Exception exception)
				{
					Log.Error($"Can not generate Model and Map files for data file: '{Path.GetFileName(file)}'", exception);
				}
			}

			// Old version
			//CodeGeneration.CreateCSharpFilesForAllCsvFiles(solution.DataFolder, nameSpace, modelFolder);

			var projectFileName = modelFolder.ToLower().Replace("model", $"{TemplateContext.Solution.Name}.Shared.projitems");

			var referenceName = "operationx";

			projectFileService.Open(projectFileName);
			var newFiles = fileSystemService.Files(modelFolder, "*.cs").ToArray();

			var toBeRemove = new List<string>();
			foreach (var file in newFiles)
			{
				try
				{
					if (file.ToLower().Contains(referenceName))
					{
						toBeRemove.Add(file);
						continue;
					}
					if (file.ToLower().EndsWith("map.cs"))
					{
						projectFileService.AddItem("Compile", $"{referenceName}.cs", $@"Import\{Path.GetFileName(file)}");
						fileSystemService.Move(file, file.Replace(@"\Model\", @"\Model\Import\"));
					}
					else
					{
						projectFileService.AddItem("Compile", $"{referenceName}.cs", Path.GetFileName(file));
					}
				}
				catch (Exception e)
				{
					Log.Error($"Can not process file: '{Path.GetFileName(file)}'", e);
				}

			}
			foreach (var file in toBeRemove)
			{
				try
				{
					fileSystemService.DeleteFile(file);
					projectFileService.RemoveItem("Compile", Path.GetFileName(file).ToLower());
				}
				catch (Exception e)
				{
					Log.Error($"Can delete file: '{file}'", e);
				}
			}
			projectFileService.Save();
		}

		private void CreateDataFiles(string root, IFileSystemService fileSystemService, IProjectFileService projectFileService)
		{
			var testFilesFolder = fileSystemService.Folders(root, "*.*").FirstOrDefault(f => f.ToLower().Contains("\\testfiles"));
			if (testFilesFolder == null)
			{
				return;
			}

			var projectFileName = testFilesFolder.ToLower().Replace("testfiles", $"{TemplateContext.Solution.Name}.Tests.Shared.projitems");

			// Copy and Add generated files:
			projectFileService.Open(projectFileName);
			var files = fileSystemService.Files(_templateContext.Data.DataFolder, "*.csv").ToArray();

			var referenceName = "operationx.csv";
			foreach (var file in files)
			{
				try
				{
					var targetFileName = Path.Combine(testFilesFolder, Path.GetFileName(file));
					fileSystemService.Copy(file, targetFileName);
					projectFileService.AddItem("None", referenceName, Path.GetFileName(file));
				}
				catch (Exception e)
				{
					Log.Error($"Can not process data file: '{Path.GetFileName(file)}'", e);
				}
			}

			projectFileService.RemoveItem("None", referenceName);
			projectFileService.Save();

			try
			{
				fileSystemService.DeleteFile(Path.Combine(testFilesFolder, referenceName));
			}
			catch (Exception e)
			{
				Log.Error("Can not delete template file", e);
			}
		}

		private void CreateSingleTestClassFile(string root, IFileSystemService fileSystemService, IProjectFileService projectFileService, IEntityPluralService entityPluralService)
		{
			var testFolder = fileSystemService.Folders(root, "*.*")
				.Where(f => f.ToLower().Contains("tests.shared"))
				.OrderBy(f => f.Length)
				.FirstOrDefault();

			if (testFolder == null)
			{
				return;
			}
			var projectFileName = testFolder.ToLower().Replace("shared", $"Shared\\{TemplateContext.Solution.Name}.Tests.Shared.projitems");

			projectFileService.Open(projectFileName);
			var files = fileSystemService.Files(_templateContext.Data.DataFolder, "*.csv").ToArray();

			var testFileName = Path.Combine(testFolder, "CsvImportTests.cs");
			var inputLines = fileSystemService.ReadAllLines(testFileName);
			var outputLines = new List<string>();

			var prologueLines = GetPrologueLines(inputLines);
			outputLines.AddRange(prologueLines);

			foreach (var file in files)
			{
				try
				{
					var className = entityPluralService.ToSingular(Path.GetFileNameWithoutExtension(file).ToCamelCase());
					var methodLines = GetTestLines(inputLines);
					ReplaceInLines(methodLines, "OperationX.csv", Path.GetFileName(file));
					ReplaceInLines(methodLines, "OperationX", className);
					outputLines.AddRange(methodLines);
				}
				catch (Exception e)
				{
					Log.Error("Can add unit test for model class {className}", e);
				}
			}
			outputLines.Add($"{new string(' ', 4)}}}");
			outputLines.Add("}");

			fileSystemService.WriteAllLines(testFileName, outputLines);
		}

		private void ReplaceInLines(IList<string> testLines, string oldValue, string y)
		{
			for (int index = 0; index < testLines.Count; index++)
			{
				testLines[index] = testLines[index].Replace(oldValue, y);
			}
		}

		private IList<string> GetTestLines(string[] sourceLines)
		{
			var result = new List<string>();
			var isTestLine = false;
			foreach (var line in sourceLines)
			{
				if (line.Contains("[Test]"))
				{
					isTestLine = true;
				}
				if (isTestLine)
				{
					result.Add((string)line.Clone());
				}
				if (line.Trim().Length == 1 && line.Contains("}"))
				{
					result.Add("");
					break;
				}
			}
			return result;
		}

		private IList<string> GetPrologueLines(IEnumerable<string> sourceLines)
		{
			return sourceLines
				.TakeWhile(line => !line.Contains("[Test]"))
				.Select(line => (string)line.Clone()).ToList();
		}
	}
}