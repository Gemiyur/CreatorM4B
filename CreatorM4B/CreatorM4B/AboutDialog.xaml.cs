using System.Reflection;
using System.Text;
using System.Windows;

namespace CreatorM4B;

/// <summary>
/// Класс окна "О программе".
/// </summary>
public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();

        string version;
        var array = assembly.GetName().Version?.ToString().Split('.');
        if (array == null || array.Length == 0)
        {
            version = "Версия не указана";
        }
        else
        {
            var count = 3;
            if (array.Length < count)
                count = array.Length;
            var sb = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                sb.Append(array[i]);
                if (i < count - 1)
                    sb.Append('.');
            }
            version = $"Версия: {sb}";
        }

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
