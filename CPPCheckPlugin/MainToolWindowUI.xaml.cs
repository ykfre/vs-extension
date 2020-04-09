using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Collections.Generic;

namespace VSPackage.CPPCheckPlugin
{
    /// <summary>
    /// Interaction logic for MainToolWindowUI.xaml
    /// </summary>
    public partial class MainToolWindowUI : UserControl
    {
        public class SuppresssionRequestedEventArgs : EventArgs
        {
            public SuppresssionRequestedEventArgs(Problem p, ICodeAnalyzer.SuppressionScope scope) { Problem = p; Scope = scope; }
            public Problem Problem { get; set; }
            public ICodeAnalyzer.SuppressionScope Scope { get; set; }
        }

        public class OpenProblemInEditorEventArgs : EventArgs
        {
            public OpenProblemInEditorEventArgs(Problem p) { Problem = p; }
            public Problem Problem { get; set; }
        }

        public delegate void suppresssionRequestedHandler(object sender, SuppresssionRequestedEventArgs e);
        public delegate void openProblemInEditor(object sender, OpenProblemInEditorEventArgs e);

        public event suppresssionRequestedHandler SuppressionRequested;
        public event openProblemInEditor EditorRequestedForProblem;

        private static int iconSize = 20;

        private GridViewColumnHeader listViewSortCol = null;

        private Dictionary<String, int> _columns_order = new Dictionary<String, int>();

        public MainToolWindowUI()
        {
            InitializeComponent();
        }

        private void menuItem_suppressThisMessageProjectWide(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessage);
        }

        private void menuItem_suppressThisMessageSolutionWide(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageSolutionWide);
        }

        private void menuItem_suppressThisMessageGlobally(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisMessageGlobally);
        }

        private void menuItem_suppressThisTypeOfMessageGlobally(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisTypeOfMessagesGlobally);
        }
        private void menuItem_suppressThisTypeOfMessageFileWide(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisTypeOfMessageFileWide);
        }

        private void menuItem_suppressThisTypeOfMessageProjectWide(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisTypeOfMessageProjectWide);
        }

        private void menuItem_suppressThisTypeOfMessageSolutionWide(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressThisTypeOfMessagesSolutionWide);
        }

        private void menuItem_suppressAllMessagesThisFileProjectWide(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressAllMessagesThisFileProjectWide);
        }

        private void menuItem_suppressAllMessagesThisFileSolutionWide(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressAllMessagesThisFileSolutionWide);
        }

        private void menuItem_suppressAllMessagesThisFileGlobally(object sender, RoutedEventArgs e)
        {
            menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope.suppressAllMessagesThisFileGlobally);
        }

        private void menuItem_SuppressSelected(ICodeAnalyzer.SuppressionScope scope)
        {
            var selectedItems = listView.SelectedItems;
            foreach (ProblemsListItem item in selectedItems)
            {
                if (item != null)
                    SuppressionRequested(this, new SuppresssionRequestedEventArgs(item.Problem, scope));
            }
        }

        private void onProblemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var objectClicked = FindVisualParent<ListViewItem, ListView>(e.OriginalSource as DependencyObject);
            if (objectClicked == null)
                return;

            ProblemsListItem item = listView.ItemContainerGenerator.ItemFromContainer(objectClicked) as ProblemsListItem;
            if (item != null)
                EditorRequestedForProblem(this, new OpenProblemInEditorEventArgs(item.Problem));
        }

        public static TParent FindVisualParent<TParent, TLimit>(DependencyObject obj) where TParent : DependencyObject
        {
            while (obj != null && !(obj is TParent))
            {
                if (obj is TLimit)
                    return null;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return obj as TParent;
        }

        private ListSortDirection sortByColumn(String column)
        {
            int counter = 0;
            _columns_order.TryGetValue(column, out counter);
            var new_dir = ListSortDirection.Ascending;
            if (counter == 0)
            {
                new_dir = ListSortDirection.Ascending;
            }
            else if (counter == 1)
            {
                new_dir = ListSortDirection.Descending;
            }
            if (0 == counter || 1 == counter)
            {
                int found_index = -1;
                for (int i = 0; i < listView.Items.SortDescriptions.Count; i++)
                {
                    if (listView.Items.SortDescriptions[i].PropertyName == column)
                    {
                        found_index = i;
                        break;
                    }
                }
                if (found_index == -1)
                {
                    listView.Items.SortDescriptions.Add(new SortDescription(column, new_dir));
                }
                else
                {
                    listView.Items.SortDescriptions[found_index] = new SortDescription(column, new_dir);

                }
            }
            else
            {
                listView.Items.SortDescriptions.Clear();
                listView.Items.SortDescriptions.Add(new SortDescription(column, new_dir));
            }
            _columns_order[column] = (counter + 1) % 3;
            return new_dir;
        }

        private void problemColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (sender as GridViewColumnHeader);
            string sortBy = column.Tag.ToString();

            var newDir = sortByColumn(sortBy);
            listViewSortCol = column;
        }

        public void ResetSorting()
        {
            CPPCheckPluginPackage.Instance.JoinableTaskFactory.Run(async () =>
            {

                await CPPCheckPluginPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();

                listView.Items.SortDescriptions.Clear();
                _columns_order.Clear();
                sortByColumn("Severity");
                sortByColumn("Message");
                sortByColumn("FileName");
            });
        }

        public class ProblemsListItem
        {
            public ProblemsListItem(Problem problem)
            {
                _problem = problem;
                Debug.Assert(problem != null);
            }

            public String Message
            {
                get { return _problem.Message; }
            }

            public String FileName
            {
                get { return _problem.FileName; }
            }

            public int Line
            {
                get { return _problem.Line; }
            }

            public int Col
            {
                get { return _problem.Col; }
            }

            public String Severity
            {
                get { return _problem.Severity; }
            }

            public ImageSource Icon
            {
                get
                {
                    Icon fromIcon = null;

                    fromIcon = SystemIcons.Warning;

                    int destWidth = iconSize;
                    int destHeight = iconSize;
                    using (Bitmap bitmap = new Bitmap(destWidth, destHeight))
                    {
                        using (Graphics graphicsSurface = Graphics.FromImage(bitmap))
                        {
                            graphicsSurface.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            using (var iconBitmap = fromIcon.ToBitmap())
                            {
                                graphicsSurface.DrawImage(iconBitmap, 0, 0, destWidth, destHeight);
                            }
                        }
                        // This obtains an unmanaged resource that must be released explicitly
                        IntPtr hBitmap = bitmap.GetHbitmap();
                        try
                        {
                            var sizeOptions = BitmapSizeOptions.FromWidthAndHeight(bitmap.Width, bitmap.Height);
                            ImageSource imgSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, sizeOptions);
                            return imgSource;
                        }
                        finally
                        {
                            DeleteObjectInvoker.DeleteObject(hBitmap);
                        }
                    }
                }
            }

            public Problem Problem
            {
                get { return _problem; }
            }

            Problem _problem;
        }
        private void ListView_SelectionChanged()
        {
        }
        private void ListView_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
        }
        private void ListView_SelectionChanged_2(object sender, SelectionChangedEventArgs e)
        {
        }
    }

    public class DeleteObjectInvoker
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }

    public class SortAdorner : Adorner
    {
        private static Geometry ascGeometry = Geometry.Parse("M 0 4 L 3.5 0 L 7 4 Z");
        private static Geometry descGeometry = Geometry.Parse("M 0 0 L 3.5 4 L 7 0 Z");

        public ListSortDirection Direction { get; private set; }

        public SortAdorner(UIElement element, ListSortDirection dir)
            : base(element)
        {
            this.Direction = dir;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (AdornedElement.RenderSize.Width < 20)
            {
                return;
            }

            TranslateTransform transform = new TranslateTransform(AdornedElement.RenderSize.Width - 15, (AdornedElement.RenderSize.Height - 5) / 2);
            drawingContext.PushTransform(transform);

            Geometry geometry = ascGeometry;
            if (this.Direction == ListSortDirection.Descending)
            {
                geometry = descGeometry;
            }
            drawingContext.DrawGeometry(System.Windows.Media.Brushes.Black, null, geometry);

            drawingContext.Pop();
        }
    }
}
