//
// MonoMakefileFormat.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MonoDevelop.Core;
using System.Text.RegularExpressions;
using MonoDevelop.Projects;
using MonoDevelop.Projects.Extensions;
using System.Threading.Tasks;

namespace MonoDeveloper
{
	public class MonoMakefileFormat: IFileFormat
	{
		public static readonly string[] Configurations = new string [] {
			"default", "net_2_0"
		};
		
		public string Name {
			get { return "Mono Makefile"; }
		}

		public FilePath GetValidFormatName (object obj, FilePath fileName)
		{
			return fileName.ParentDirectory.Combine ("Makefile");
		}

		public bool CanReadFile (FilePath file, Type expectedType)
		{
			if (file.FileName != "Makefile") return false;
			MonoMakefile mkfile = new MonoMakefile (file);
			if (mkfile.Content.IndexOf ("build/rules.make") == -1) return false;
			
			if (mkfile.GetVariable ("LIBRARY") != null) return expectedType.IsAssignableFrom (typeof(DotNetProject));
			if (mkfile.GetVariable ("PROGRAM") != null) return expectedType.IsAssignableFrom (typeof(DotNetProject));
			string subdirs = mkfile.GetVariable ("SUBDIRS");
			if (subdirs != null && subdirs.Trim (' ','\t') != "")
				return expectedType.IsAssignableFrom (typeof(Solution)) || expectedType.IsAssignableFrom (typeof(SolutionFolder));
			
			return false;
		}
		
		public bool CanWriteFile (object obj)
		{
			return (obj is SolutionFolder) || (obj is MakefileProject);
		}

		public Task WriteFile (FilePath file, object node, ProgressMonitor monitor)
		{
			return Task.FromResult (0);
		}

		public List<FilePath> GetItemFiles (object obj)
		{
			List<FilePath> col = new List<FilePath> ();
			var mp = obj as MakefileProject;
			if (mp != null) {
				if (File.Exists (mp.SourcesFile)) {
					col.Add (mp.FileName);
					col.Add (mp.SourcesFile);
				}
			}
			return col;
		}

		public Task<object> ReadFile (FilePath fileName, Type expectedType, ProgressMonitor monitor)
		{
			return ReadFile (fileName, false, monitor);
		}

		public Task<object> ReadFile (FilePath fileName, bool hasParentSolution, ProgressMonitor monitor)
		{
			return Task<object>.Factory.StartNew (delegate {
				FilePath basePath = fileName.ParentDirectory;
				MonoMakefile mkfile = new MonoMakefile (fileName);
				string aname = mkfile.GetVariable ("LIBRARY");
				if (aname == null)
					aname = mkfile.GetVariable ("PROGRAM");
			
				try {
					ProjectExtensionUtil.BeginLoadOperation ();
					if (aname != null) {
						// It is a project
						monitor.BeginTask ("Loading '" + fileName + "'", 0);
						MakefileProject project = new MakefileProject ("C#");
						project.Name = Path.GetFileName (basePath);
						project.Read (mkfile);
						monitor.EndTask ();
						return project;
					} else {
						string subdirs;
						StringBuilder subdirsBuilder = new StringBuilder ();
						subdirsBuilder.Append (mkfile.GetVariable ("common_dirs"));
						if (subdirsBuilder.Length != 0) {
							subdirsBuilder.Append ("\t");
							subdirsBuilder.Append (mkfile.GetVariable ("net_2_0_dirs"));
						}
						if (subdirsBuilder.Length == 0)
							subdirsBuilder.Append (mkfile.GetVariable ("SUBDIRS"));
	
						subdirs = subdirsBuilder.ToString ();
						if (subdirs != null && (subdirs = subdirs.Trim (' ', '\t')) != "") {
							object retObject;
							SolutionFolder folder;
							if (!hasParentSolution) {
								Solution sol = new Solution ();
								sol.ConvertToFormat (Services.ProjectService.FileFormats.GetFileFormat ("MonoMakefile"), false);
								sol.FileName = fileName;
								folder = sol.RootFolder;
								retObject = sol;
							
								foreach (string conf in MonoMakefileFormat.Configurations) {
									SolutionConfiguration sc = new SolutionConfiguration (conf);
									sol.Configurations.Add (sc);
								}
							} else {
								folder = new SolutionFolder ();
								folder.Name = Path.GetFileName (Path.GetDirectoryName (fileName));
								retObject = folder;
							}
						
							subdirs = subdirs.Replace ('\t', ' ');
							string[] dirs = subdirs.Split (' ');
						
							monitor.BeginTask ("Loading '" + fileName + "'", dirs.Length);
							Hashtable added = new Hashtable ();
							foreach (string dir in dirs) {
								if (added.Contains (dir))
									continue;
								added.Add (dir, dir);
								monitor.Step (1);
								if (dir == null)
									continue;
								string tdir = dir.Trim ();
								if (tdir == "")
									continue;
								string mfile = Path.Combine (Path.Combine (basePath, tdir), "Makefile");
								if (File.Exists (mfile) && CanReadFile (mfile, typeof(SolutionFolderItem))) {
									SolutionFolderItem it = (SolutionFolderItem)ReadFile (mfile, true, monitor).Result;
									folder.Items.Add (it);
								}
							}
							monitor.EndTask ();
							return retObject;
						}
					}
				} finally {
					ProjectExtensionUtil.EndLoadOperation ();
				}
				return null;
			});
		}
		
		public Task ConvertToFormat (object obj)
		{
			return Task.FromResult (0);
		}
		
		public bool SupportsMixedFormats {
			get { return false; }
		}
		
		public IEnumerable<string> GetCompatibilityWarnings (object obj)
		{
			yield break;
		}
		
		public bool SupportsFramework (MonoDevelop.Core.Assemblies.TargetFramework framework)
		{
			return true;
		}
	}
}
