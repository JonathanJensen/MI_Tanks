/*****************************************************************************
*       Copyright © 2015 Pitney Bowes Software Inc.
*       All rights reserved.
*****************************************************************************/

using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using MapInfo.Types;
using MapInfo.Controls;
using System.Threading;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;

namespace MI_Tanks
{
	public class MI_TanksAddIn : IMI_TanksAddIn
	{
		protected IMapInfoPro MapInfoApplication;
		private IRibbonTab homeTab;
		private IRibbonControlGroup autoRefresherControlsGroup;
		private IRibbonButtonControl _autoRefresherBtnCtr;
        //private MainForm mainForm = null;
        NetworkStream serverStream = null;

        private void SendMessage(string message)
        {
            Debug.WriteLine(message);
            byte[] outStream = System.Text.Encoding.ASCII.GetBytes(message);
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
        }

        public void Initialize(IMapInfoPro mapInfoApplication, string mbxname)
		{
			UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), "pack", -1);

			MapInfoApplication = mapInfoApplication;
			ThisApplication = MapInfoApplication.GetMapBasicApplication(mbxname);

            MapInfoApplication.RunMapBasicCommand(
				string.Format("print \"app domain id {0}, mbxname {1}\"", AppDomain.CurrentDomain.Id, ThisApplication.Name), false);
            // Add a button to the open gallery in Home Tab.
            
            AddButtonToOpenGalleryInHomeTab();
		}

		void OnEndApplication(object sender)
		{
			if (ThisApplication != null)
			{
				ThisApplication.EndApplication();
			}
		}

		private void OnMBHandlerClick(object param)
		{
			ThisApplication.CallMapBasicSubroutine("CustomHandler", "Hello World From CustomHandler");
		}

		//private void OnWindowGalleryRibbonItemClicked(object sender)
		//{
  //          mainForm = new MainForm();
  //          mainForm.Show();
  //          //MessageBox.Show("The Window Gallery Demo Ribbon Item was clicked.");
  //      }

		private void AddButtonToOpenGalleryInHomeTab()
		{
            MapInfoApplication.RunMapBasicCommand("print \"Adding button to homeTab\"", false);
            //Access the home tab.
            if (MapInfoApplication.Ribbon.Tabs == null || MapInfoApplication.Ribbon.Tabs.Count <= 0)
            {
                MapInfoApplication.RunMapBasicCommand("print \"no homeTab\"", false);
                return;
            }

			homeTab = MapInfoApplication.Ribbon.Tabs[0];
            if (homeTab == null)
            {
                MapInfoApplication.RunMapBasicCommand("print \"homeTab = null\"", false);

                return;
            }

            autoRefresherControlsGroup = homeTab.Groups.Add("Tanks", "Tank game");
            if (autoRefresherControlsGroup == null) return;

            //Set the tooltip for the Launcher button available on the right corner of any new control group
            autoRefresherControlsGroup.LauncherToolTip = new MapInfoRibbonToolTip()
            {
                //ToolTipDescription = "Demo Controls Launcher Tooltip Description",
                //ToolTipText = "Demo Controls Launcher ToolTip Text",
                //ToolTipDisabledText = "Demo Controls Launcher Disabled Text"
            };

            _autoRefresherBtnCtr =
                autoRefresherControlsGroup.Controls.Add("Tanks", "Tanks game") as IRibbonButtonControl;
			if(_autoRefresherBtnCtr == null)
            {
                MapInfoApplication.RunMapBasicCommand("print \"_demoButtonControlGalleryItem = null\"", false);
                return;
            }

            _autoRefresherBtnCtr.IsLarge = true;
			_autoRefresherBtnCtr.Width = 70;
			_autoRefresherBtnCtr.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
			_autoRefresherBtnCtr.VerticalAlignment = VerticalAlignment.Top;
			_autoRefresherBtnCtr.KeyTip = "O";

			//Set the tooltip
			_autoRefresherBtnCtr.ToolTip = new MapInfoRibbonToolTip()
			{
				ToolTipDescription = "Open the tanks game",
				ToolTipText = "Open the tanks game",
				ToolTipDisabledText = "Open the tanks game",
				Placement = PlacementMode.Bottom,
				VerticalOffSet = 5
			};

			//We are going to wrap the function which needs to be called when the button is clicked in a Delegate Command.
			_autoRefresherBtnCtr.Command = new DelegateCommand(OnOpenAutoRefresherBtnCtrClicked).ViewToContractAdapter();

			_autoRefresherBtnCtr.LargeIcon =
						PathToUri("pack://application:,,,/MapInfo.StyleResources;component/Images/Mapping/infoTool_32x32.png");
			_autoRefresherBtnCtr.SmallIcon =
				PathToUri("pack://application:,,,/MapInfo.StyleResources;component/Images/Mapping/infoTool_16x16.png");
		}

		void OnOpenAutoRefresherBtnCtrClicked(object sender)
		{
            //mainForm = new MainForm(MapInfoApplication);
            //mainForm.Show();
            UsernameForm unf = new UsernameForm(MapInfoApplication);
            unf.Show();

            //MessageBox.Show("The Open Gallery demo button was clicked.");
        }

        public virtual void Unload()
		{
			UnloadButtonAddedToHomeTab();
			_autoRefresherBtnCtr = null;
            homeTab = null;
		}

		private void UnloadButtonAddedToHomeTab()
		{
			if (autoRefresherControlsGroup == null) return;

			if (_autoRefresherBtnCtr != null)
                autoRefresherControlsGroup.Controls.Remove(_autoRefresherBtnCtr);

            homeTab.Groups.Remove(autoRefresherControlsGroup);
        }

		public IMapBasicApplication ThisApplication { get; set; }

        public static Uri PathToUri(string uri)
		{
			try
			{
				return new Uri(uri, UriKind.RelativeOrAbsolute);
			}
			catch (Exception)
			{
				return null;
			}
		}

    }
}
