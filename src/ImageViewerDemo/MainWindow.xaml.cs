using System.Windows;

namespace ImageViewerDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void refreshBtn_Click(object sender, RoutedEventArgs e)
        {
            string img1 = "Images/img1.jpg";
            string img2 = "Images/img2.jpg";

            if (this.Tag as string == img2)
                this.Tag = img1;
            else
                this.Tag = img2;
        }

        private void leftBtn_Click(object sender, RoutedEventArgs e)
        {
            imageViewer.Left(100);
        }

        private void rightBtn_Click(object sender, RoutedEventArgs e)
        {
            imageViewer.Right(100);
        }

        private void upBtn_Click(object sender, RoutedEventArgs e)
        {
            imageViewer.Up(100);
        }

        private void downBtn_Click(object sender, RoutedEventArgs e)
        {
            imageViewer.Down(100);
        }

        private void zoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            imageViewer.ZoomIn();
        }

        private void zoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            imageViewer.ZoomOut();
        }

        private void resetBtn_Click(object sender, RoutedEventArgs e)
        {
            imageViewer.Reset();
        }
    }
}