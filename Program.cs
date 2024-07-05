using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static AutoHider.Program;

namespace AutoHider
{
    internal class Program
    {
        const int SW_MINIMIZE = 6;                  // Minimize Window
        const int WM_CLOSE = 0x0010;                // Close Window
        const int BM_CLICK = 0x00F5;                // Click Button
        const int WM_KEYDOWN = 0x0100;              // Press a key
        const int WM_KEYUP = 0x0101;                // Release a key

        const int VK_RETURN = 0x0D;         // Enter Key

        const string WECHAT_LOGIN_WINDOW_CLASS = "WeChatLoginWndForPC";     // Window class name of WeChat login window
        const string WECHAT_MAIN_WINDOW_CLASS = "WeChatMainWndForPC";       // Window class name of WeChat main window
        const string QQ_MAIN_WINDOW_CLASS = "Chrome_WidgetWin_1";           // Window class name of QQ(NT) main windows

        const string WECHAT_PROCESS_NAME = "WeChat";    // Process name of WeChat
        const string QQ_PROCESS_NAME = "QQ";            // Process name of QQ

        #region DllImport
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        #endregion DllImport

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        static void Main(string[] args)
        {
            bool isWeChatLogined = false, isWeChatClosed = false, isQQClosed = false;
            int RetryCounter = 0;
            while (!(isWeChatLogined && isWeChatClosed && isQQClosed))
            {
                RetryCounter++;
                if(!isWeChatLogined)
                    isWeChatLogined = SendKey(WECHAT_LOGIN_WINDOW_CLASS, null, VK_RETURN, WECHAT_PROCESS_NAME);
                if (!isWeChatClosed)
                    isWeChatClosed = CloseWindowWithoutQQLogin(WECHAT_MAIN_WINDOW_CLASS, null, WECHAT_PROCESS_NAME);
                if (!isQQClosed)
                    isQQClosed = CloseWindowWithoutQQLogin(QQ_MAIN_WINDOW_CLASS, null, QQ_PROCESS_NAME);
                Thread.Sleep(500);

                if(RetryCounter > 120)
                {
                    return;
                }
            }
        }

        static string GetProcessName(IntPtr hWnd)
        {
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);
            Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }

        static List<IntPtr> FindVisibleWindowsAccurately(string WindowClass, string WindowTitle, string ProcessName)
        {
            List<IntPtr> windows = new List<IntPtr>();

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder currentWindowClass = new StringBuilder { Capacity = 64 };
                GetClassName(hWnd, currentWindowClass, currentWindowClass.Capacity);

                StringBuilder currentWindowTitle = new StringBuilder { Capacity = 64 };
                GetWindowText(hWnd, currentWindowTitle, currentWindowTitle.Capacity);

                bool isWindowClassEquals = WindowClass == null ? true :
                    currentWindowClass.ToString().Equals(WindowClass, StringComparison.OrdinalIgnoreCase);
                bool isWindowTitleEquals = WindowTitle == null ? true :
                    currentWindowTitle.ToString().Equals(WindowTitle, StringComparison.OrdinalIgnoreCase);

                if (isWindowClassEquals &&
                    isWindowTitleEquals &&
                    GetProcessName(hWnd).Equals(ProcessName, StringComparison.OrdinalIgnoreCase) &&
                    IsWindowVisible(hWnd))
                {
                    windows.Add(hWnd);
                }

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        static bool MinimizeWindow(string WindowClass, string WindowTitle, string ProcessName)
        {
            foreach(IntPtr hWnd in FindVisibleWindowsAccurately(WindowClass, WindowTitle, ProcessName))
            {
                ShowWindow(hWnd, SW_MINIMIZE);
                return true;
            }
            return false;
        }

        static bool CloseWindowWithoutQQLogin(string WindowClass, string WindowTitle, string ProcessName)
        {
            foreach (IntPtr hWnd in FindVisibleWindowsAccurately(WindowClass, WindowTitle, ProcessName))
            {
                RECT windowRect;
                GetWindowRect(hWnd, out windowRect);
                int windowHeight = windowRect.Bottom - windowRect.Top;
                int windowWidth = windowRect.Right - windowRect.Left;
                if (windowWidth != 320 && windowHeight != 448)
                {
                    if(GetProcessName(hWnd) == QQ_PROCESS_NAME)
                        Thread.Sleep(2000);
                    SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
            }
            return false;
        }

        static bool ClickButton(string WindowClass, string WindowTitle, string ButtonText, string ProcessName)
        {
            foreach (IntPtr hWnd in FindVisibleWindowsAccurately(WindowClass, WindowTitle, ProcessName))
            {
                IntPtr hButton = FindWindowEx(hWnd, IntPtr.Zero, null, ButtonText);
                if (hButton != IntPtr.Zero)
                {
                    SendMessage(hButton, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
            }
            return false;
        }

        static bool SendKey(string WindowClass, string WindowTitle, int Key, string ProcessName)
        {
            foreach (IntPtr hWnd in FindVisibleWindowsAccurately(WindowClass, WindowTitle, ProcessName))
            {
                PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
                PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);
                return true;
            }
            return false;
        }
    }
}
