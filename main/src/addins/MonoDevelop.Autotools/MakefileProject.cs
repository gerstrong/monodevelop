
using System;
using MonoDevelop.Projects;
using System.Collections.Generic;

namespace MonoDevelop.Autotools
{
	public class MakefileProject: Project
	{
		public MakefileProject()
		{
		}
		
		public override SolutionItemConfiguration CreateConfiguration (string name)
		{
			MakefileProjectConfiguration conf = new MakefileProjectConfiguration ();
			conf.Name = name;
			return conf;
		}

		protected override void OnGetProjectTypes (HashSet<string> types)
		{
			types.Add ("MakefileProject");
		}
	}
	
	public class MakefileProjectConfiguration: ProjectConfiguration
	{
	}
}
