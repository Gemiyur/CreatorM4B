using System.Reflection;
using System.Windows;

namespace CreatorM4B
{
    /// <summary>
    /// Класс окна "О программе".
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();
            var array = assembly.GetName().Version.ToString().Split('.');
            var version = $"Версия: {array[0]}.{array[1]}.{array[2]}";
            var product =
                ((AssemblyProductAttribute)assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), true)
                .First()).Product;
            var description =
                ((AssemblyDescriptionAttribute)assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), true)
                .First()).Description.Replace("\r\n", "\n");
            var copyright =
                ((AssemblyCopyrightAttribute)assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), true)
                .First()).Copyright;

            ProductTextBlock.Text = product;
            DescriptionTextBlock.Text = description;
            CopyrightTextBlock.Text = copyright;
            VersionTextBlock.Text = version;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
