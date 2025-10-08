using System;
using System.Diagnostics;
using System.Linq;

using Skyline.AppInstaller;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Core.DataMinerSystem.Automation;
using Skyline.DataMiner.Core.DataMinerSystem.Common;
using Skyline.DataMiner.Net.AppPackages;

/// <summary>
/// DataMiner Script Class.
/// </summary>
internal class Script
{
	/// <summary>
	/// The script entry point.
	/// </summary>
	/// <param name="engine">Provides access to the Automation engine.</param>
	/// <param name="context">Provides access to the installation context.</param>
	[AutomationEntryPoint(AutomationEntryPointType.Types.InstallAppPackage)]
	public void Install(IEngine engine, AppInstallContext context)
	{
		try
		{
			engine.Timeout = new TimeSpan(0, 10, 0);
			engine.GenerateInformation("Starting installation");

			var installer = new AppInstaller(Engine.SLNetRaw, context);
			installer.InstallDefaultContent();

			////string setupContentPath = installer.GetSetupContentDirectory();

			var dms = engine.GetDms();
			
			var view = CreateViews(dms);
			CreateElements(dms, view);
		}
		catch (Exception e)
		{
			engine.ExitFail($"Exception encountered during installation: {e}");
		}
	}

	private IDmsView CreateViews(IDms dms)
	{
		var rootView = dms.GetView(-1);

		var viewTutorials = GetOrCreateView(dms, rootView, "Tutorials");
		var viewTutorialIpMatrix = GetOrCreateView(dms, viewTutorials, "Tutorial-IPMatrix");

		return viewTutorialIpMatrix;
	}

	private IDmsView GetOrCreateView(IDms dms, IDmsView parent, string name)
	{
		if (dms.ViewExists(name))
		{
			return dms.GetView(name);
		}
		else
		{
			var viewId = dms.CreateView(new ViewConfiguration(name, parent));
			WaitUntil(() => dms.ViewExists(viewId), TimeSpan.FromSeconds(30));

			return dms.GetView(viewId);
		}
	}

	private void CreateElements(IDms dms, IDmsView view)
	{
		var dma = dms.GetAgents().First();

		for (int i = 1; i <= 4; i++)
		{
			CreateEncoderElement(dma, view, $"Encoder - {i}", $"239.1.{i}.1");
			CreateDecoderElement(dma, view, $"Decoder - {i}");
		}
	}

	private IDmsElement CreateEncoderElement(IDma dma, IDmsView view, string name, string multicastIp)
	{
		var element = CreateElement(dma, view, name);

		CreateEntryInGenericTableElement(element, "IP Out", multicastIp);

		return element;
	}

	private IDmsElement CreateDecoderElement(IDma dma, IDmsView view, string name)
	{
		var element = CreateElement(dma, view, name);

		CreateEntryInGenericTableElement(element, "IP In", String.Empty);

		return element;
	}

	private IDmsElement CreateElement(IDma dma, IDmsView view, string name)
	{
		var dms = dma.Dms;

		if (dms.ElementExists(name))
		{
			return dms.GetElement(name);
		}

		var protocol = dms.GetProtocol("Generic Dynamic Table", "1.0.0.4");

		var elementConfig = new ElementConfiguration(dms, name, protocol);
		elementConfig.Views.Add(view);

		var id = dma.CreateElement(elementConfig);
		WaitUntil(() => dms.ElementExists(id), TimeSpan.FromSeconds(30));

		var element = dms.GetElement(id);
		WaitUntil(() => element.IsStartupComplete(), TimeSpan.FromSeconds(30));

		return element;
	}

	private void CreateEntryInGenericTableElement(IDmsElement element, string key, string value)
	{
		var entriesTable = element.GetTable(200);

		var displayKeys = entriesTable.GetDisplayKeys();
		if (displayKeys.Contains(key))
		{
			return;
		}

		// Press add button
		element.GetStandaloneParameter<int?>(295).SetValue(0);

		// Wait until new row is added
		string newDisplayKey = null;

		WaitUntil(() =>
		{
			newDisplayKey = entriesTable.GetDisplayKeys().Except(displayKeys).FirstOrDefault();
			return newDisplayKey != null;
		}, TimeSpan.FromSeconds(30));

		// Set key and value
		entriesTable.GetColumn<string>(202).SetValue(newDisplayKey, KeyType.DisplayKey, key);
		entriesTable.GetColumn<string>(205).SetValue(newDisplayKey, KeyType.DisplayKey, value);
	}

	private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
	{
		var stopwatch = Stopwatch.StartNew();

		while (true)
		{
			if (condition())
			{
				return;
			}

			if (stopwatch.Elapsed >= timeout)
			{
				throw new TimeoutException();
			}

			System.Threading.Thread.Sleep(100);
		}
	}
}