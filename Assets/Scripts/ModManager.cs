using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

public class ModManager : MonoBehaviour{

	public string modsPath = "Mods";
	public List<Mod> mods = new List<Mod>();

	static ModManager _instance;
	public static ModManager Instance {get{return _instance;}}

	void Awake(){
		if(_instance != null){
			// Already instantiated
			Destroy(gameObject);
			return;
		}
		_instance = this;
	}

	void Start(){
		LoadMods();
		
		CompileMods();
		
		EnableMods();
	}

	public void EnableMods(){
		UIManager.AutoAddMessage("Enabling mods...");
		foreach(var mod in mods){
			Debug.Log("Enabling "+mod.info.name);
			UIManager.AutoAddMessage("Enabling " + mod.info.name);

			Assembly assembly = mod.LoadAssembly();
			if(assembly != null){
				System.Type[] types = assembly.GetTypes();
				foreach(var type in types){
					var imod = type.GetInterface("IMod");
					if(imod != null){
						var obj = System.Activator.CreateInstance(type) as IMod;
						if(obj != null){
							obj.OnEnable();
						}
					}
				}
			}
		}
	}

	public void LoadMods(){
		UIManager.AutoAddMessage("Loading mods...");
		
		string[] modFolders = Directory.GetDirectories(modsPath);
		foreach(string modFolder in modFolders){
			LoadMod(modFolder);
		}
	}

	public void LoadMod(string path){
		Mod mod = new Mod(path);
		mod.LoadInfo();
		mods.Add(mod);
		UIManager.AutoAddMessage("Finished loading modinfo for " + mod.info.name + " version "+mod.info.version);
	}

	public void CompileMods(){
		UIManager.AutoAddMessage("Compiling mods...");
		foreach(var mod in mods){
			mod.Compile(true);
		}
	}

	public static string GetCompiler(){
		string compilerScript = "Mono/bin/smcs";
		if(Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer){
			compilerScript += ".bat";
		}
		return  compilerScript;
	}


	// Returns platform dependent compiler args
	public static string GetPlatformCompilerArgs(){
		
		// For loading mods in the unity editor, the built assemblies are scattered all over the world! .. not really just in the install dir
	#if UNITY_EDITOR
		string libPaths = UnityEditor.EditorApplication.applicationContentsPath + "/Managed/,";
		libPaths += UnityEditor.EditorApplication.applicationContentsPath+"/UnityExtensions/Unity/GUISystem/,Library/ScriptAssemblies/";
	#else
		// Little bit easier then
		string libPaths = Application.dataPath + "/Managed/";
	#endif

		// Add more as needed
		string references = "UnityEngine.dll,Assembly-CSharp.dll,UnityEngine.UI.dll";

		return "-lib:\""+libPaths + "\" -r:\""+references+"\"";
	}
}

[System.Serializable]
public class ModInfo {
	public string name;
	public string author;
	public string version;
	public string description; 

	public override string ToString(){
		return "{Name: "+ name + ", Author: " + author + ", Version: " + version + ", Description: " + description + "}";
    }
}

public class Mod {
	public string path;
	public ModInfo info;
	public Assembly assembly;
	List<string> compilerOutput;

	public Mod(string path){
		this.path = path;
	}

	public void LoadInfo(){
		string infoJson = File.ReadAllText(path + "/mod.json");
		info = JsonConvert.DeserializeObject<ModInfo>(infoJson);
	}

	public string GetAssemblyName(){
		var split = path.Split('/');
		return split[split.Length-1] + ".dll";
	}

	public bool IsCompiled(){
		return File.Exists(path + "/" + GetAssemblyName());
	}

	public Assembly LoadAssembly(){
		if(!IsCompiled()){
			UIManager.AutoAddMessage("[" + info.name + "]" + "Failed loading assembly: Not compiled");
			Debug.LogError("Attempted to load not compiled mod");
			return null;
		}
		assembly = Assembly.LoadFrom(path +"/"+ GetAssemblyName());
		return assembly;
	}

	// ./Mono/bin/smcs -lib:/opt/UnityBeta/Editor/Data/Managed,Library/ScriptAssemblies -r:UnityEngine.dll,Assembly-CSharp.dll -t:library -out:Mods/sample/ModAssembly.dll Mods/sample/Sources/hmm.cs
	// Note this is simple compilation (with only own assembly-csharp.dll and unityengine.dll referenced),
	// if people want to make more advanced mods (include other references) they need to compile them on their own
	public void Compile(bool overwrite){
		if(IsCompiled() && !overwrite){
			Debug.Log("Already compiled " + info.name);
			return;
		}
		var sources = GetSources();

		string args = ModManager.GetPlatformCompilerArgs();
		args += " -t:library -out:\""+path+"/"+GetAssemblyName()+"\"";

		foreach(string sourcefile in sources){
			args += " \"" + sourcefile + "\"";
		}

		UIManager.AutoAddMessage("Executing " + ModManager.GetCompiler()+ " " + args);
        compilerOutput = new List<string>();

        var compileProcess = new System.Diagnostics.Process();
        compileProcess.StartInfo.FileName = ModManager.GetCompiler();
        compileProcess.StartInfo.Arguments = args;


        // Set UseShellExecute to false for redirection.
        compileProcess.StartInfo.UseShellExecute = false;

        // Redirect the standard output of the sort command.  
        // This stream is read asynchronously using an event handler.
        compileProcess.StartInfo.RedirectStandardOutput = true;
        compileProcess.StartInfo.RedirectStandardError = true;

        // Set our event handler to asynchronously read the sort output.
        compileProcess.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(OnCompilerOutput);
        compileProcess.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(OnCompilerError);

        // Start the process.
        compileProcess.Start();
        compileProcess.BeginErrorReadLine();
        compileProcess.BeginOutputReadLine();
        compileProcess.WaitForExit();
        lock(compilerOutput){
	        foreach(var line in compilerOutput){
	        	if(string.IsNullOrEmpty(line))
	        		return;

				UIManager.Instance.AddMessage(line);
	        }
        }
		UIManager.Instance.AddMessage("Compiler exited code " + compileProcess.ExitCode);    	
  	}

	public List<string> GetSources(){
		string startPath = path + "/Sources";
		return GetSources(startPath);
	}

	List<string> GetSources(string folder){
		string[] files = Directory.GetFiles(folder, "*.cs");
		List<string> fileList = new List<string>(files);
		
		string[] folders = Directory.GetDirectories(folder);
		foreach(string inner in folders){
			fileList.AddRange(GetSources(inner));
		}
		return fileList;
	}

	void OnCompilerOutput(System.Object sender, System.Diagnostics.DataReceivedEventArgs outLine){
		string str = "[" + info.name + "] compiler output: " + outLine.Data;
		Debug.Log(str);
		lock(compilerOutput){
			compilerOutput.Add(str);
		}
	}
	void OnCompilerError(System.Object sender, System.Diagnostics.DataReceivedEventArgs outLine){
		string str = "[" + info.name + "] compiler output: " + outLine.Data;
		Debug.Log(str);
		lock(compilerOutput){
			compilerOutput.Add("<color=red>" + str + "</color>");
		}
//		UIManager.Instance.AddMessage("[" + info.name + "] compiler output: " + outLine.Data);
	}
}

// Mods needs 1 class that implments this
public interface IMod {
	void OnEnable();
	void OnDisable();
}