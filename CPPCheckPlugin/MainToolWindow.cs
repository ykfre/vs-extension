﻿using System;
using System.Windows;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Text;

using System.ComponentModel;
using System.Drawing;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace VSPackage.CPPCheckPlugin
{
	[Guid("98C5C8D0-34D9-406F-AA2E-C85B47C9F268")]
	public sealed class MainToolWindow : ToolWindowPane
	{
		public MainToolWindow() : base(null)
		{
			_listView = _ui.listView;
			_ui.EditorRequestedForProblem += openProblemInEditor;

			Caption = "Cppcheck analysis results";
			Content = _ui;
		}

		public static MainToolWindow Instance
		{
			get {
				var package = CPPCheckPluginPackage.Instance;
				// Searching for existing window 
				MainToolWindow window = package.FindToolWindow(typeof(MainToolWindow), 0, false) as MainToolWindow;
				if (window != null)
					return window;
				else
					// Creating the tool window
					return package.FindToolWindow(typeof(MainToolWindow), 0, true) as MainToolWindow;
			}
		}

		public void bringToFront()
		{
            CPPCheckPluginPackage.Instance.JoinableTaskFactory.Run(async () =>
            {
                await CPPCheckPluginPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();
                IVsWindowFrame frame = Frame as IVsWindowFrame;
                if (frame == null)
                    return;

                frame.Show();
            });
            
		}
        

		public void clear()
		{
            _listView.Items.Clear();
            _listView.Items.SortDescriptions.Clear();
        }

        public bool isEmpty()
		{
			return _listView.Items.Count == 0;
		}

		private void AutoSizeColumns()
		{
			// As per http://stackoverflow.com/questions/845269/force-resize-of-gridview-columns-inside-listview
			GridView gv = _listView.View as GridView;
			if (gv != null)
			{
				for (int i = 0; i < gv.Columns.Count - 1; ++i) // The last column is message,  which should fit the rest of the window
				{
					var c = gv.Columns[i];
					// Code below was found in GridViewColumnHeader.OnGripperDoubleClicked() event handler (using Reflector)
					// i.e. it is the same code that is executed when the gripper is double clicked
					if (double.IsNaN(c.Width))
					{
						c.Width = c.ActualWidth;
					}
					c.Width = double.NaN;
				}
			}
		}

		public void displayProblem(Problem problem, bool autoSize)
		{
			CPPCheckPluginPackage.Instance.JoinableTaskFactory.Run(async () =>
			{
				try
				{
					await CPPCheckPluginPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();

					_listView.Items.Add(new MainToolWindowUI.ProblemsListItem(problem));
					if (autoSize)
						AutoSizeColumns();
				}
				catch (Exception e)
				{
					Debug.WriteLine("Exception occurred in displayProblemAsync:" + e.Message);
				}
			});
		}

		public ICodeAnalyzer.AnalysisType ContentsType
		{
			get;
			set;
		}

		private void openProblemInEditor(object sender, MainToolWindowUI.OpenProblemInEditorEventArgs e)
		{
			CPPCheckPluginPackage.Instance.JoinableTaskFactory.Run(async () =>
			{
				await CPPCheckPluginPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();

				Problem problem = e.Problem;
				IVsUIShellOpenDocument shellOpenDocument = (IVsUIShellOpenDocument)GetService(typeof(IVsUIShellOpenDocument));
				Debug.Assert(shellOpenDocument != null);
				Guid guidCodeView = VSConstants.LOGVIEWID.Code_guid;
				Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp = null;
				IVsUIHierarchy hierarchy = null;
				uint itemId = 0;
				IVsWindowFrame windowFrame = null;
				if (shellOpenDocument.OpenDocumentViaProject(problem.FilePath, ref guidCodeView, out sp, out hierarchy, out itemId, out windowFrame) != VSConstants.S_OK)
				{
					Debug.WriteLine("Error opening file " + problem.FilePath);
					return;
				}

				Debug.Assert(windowFrame != null);
				windowFrame.Show();

				EnvDTE.DTE dte = (EnvDTE.DTE)GetService(typeof(SDTE));
				Debug.Assert(dte != null);
				Debug.Assert(dte.ActiveDocument != null);
				var selection = (EnvDTE.TextSelection)dte.ActiveDocument.Selection;
				Debug.Assert(selection != null);
				selection.GotoLine(problem.Line > 0 ? problem.Line : 1); // Line cannot be 0 here
			});
		}

		public MainToolWindowUI _ui = new MainToolWindowUI();
		private ListView _listView = null;
	}
}
