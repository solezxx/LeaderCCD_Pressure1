using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Configuration;
using System.Text.RegularExpressions;



namespace LeaderCCD
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindowViewModel M { get; set; } = new MainWindowViewModel();
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = M;
            TimesBox.Text= ConfigurationManager.AppSettings["TimesBox"];
            TimeBox.Text= ConfigurationManager.AppSettings["TimeBox"];
            PreBox.Text= ConfigurationManager.AppSettings["PreBox"];
            PreBoxMin.Text= ConfigurationManager.AppSettings["PreBoxMin"];
            SpeedBox.Text= ConfigurationManager.AppSettings["SpeedBox"];
            TimesBox1.Text= ConfigurationManager.AppSettings["TimesBox1"];
            M.minPress = Convert.ToInt32(ConfigurationManager.AppSettings["PreBoxMin"]);
            M.maxPress = Convert.ToInt32(ConfigurationManager.AppSettings["PreBox"]);
        }
        /// <summary>
        /// 是否是纯数字
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool isNumber(string str)
        {
            bool isMatch = Regex.IsMatch(str, @"^\d+$"); // 判断字符串是否为数字 的正则表达式
            return isMatch;
        }
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string mes = "";
                bool res = true;
                //if (isNumber(TimesBox.Text)&& (Convert.ToInt32(TimesBox.Text) < 5 || Convert.ToInt32(TimesBox.Text) > 3000))
                //{
                //    mes += "气缸动作次数值错误\r\n";
                //    res = false;
                //}
                //if (isNumber(TimeBox.Text) && (Convert.ToInt32(TimeBox.Text) < 5 || Convert.ToInt32(TimeBox.Text) > 3000))
                //{
                //    mes += "置位时间值错误\r\n";
                //    res = false;
                //}
                if (!isNumber(PreBox.Text)|| !isNumber(SpeedBox.Text)|| !isNumber(TimesBox1.Text)||!isNumber(PreBoxMin.Text))
                {
                    MessageBox.Show("有参数不是数字");
                    return;
                }
                if ( Convert.ToInt32(PreBox.Text) < 5 || Convert.ToInt32(PreBox.Text) > 3000|| Convert.ToInt32(PreBoxMin.Text) < 5 || Convert.ToInt32(PreBoxMin.Text) > 3000)
                {
                    mes += "压力值错误\r\n";
                    res = false;
                }
                if ( Convert.ToInt32(SpeedBox.Text) < 100 || Convert.ToInt32(SpeedBox.Text) > 3000)
                {
                    mes += "速度值错误\r\n";
                    res = false;
                }
                if (Convert.ToInt32(TimesBox1.Text) < 1 || Convert.ToInt32(TimesBox1.Text) > 2000000000)
                {
                    mes += "次数值错误\r\n";
                    res = false;
                }
                if (res)
                {
                    M.cfa.AppSettings.Settings["TimesBox"].Value = TimesBox.Text;
                    M.cfa.AppSettings.Settings["TimeBox"].Value = TimeBox.Text;
                    M.cfa.AppSettings.Settings["PreBox"].Value = PreBox.Text;
                    M.cfa.AppSettings.Settings["PreBoxMin"].Value = PreBoxMin.Text;
                    M.cfa.AppSettings.Settings["SpeedBox"].Value = SpeedBox.Text;
                    M.cfa.AppSettings.Settings["TimesBox1"].Value = TimesBox1.Text;
                    M.cfa.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    int x1 = Convert.ToInt32(TimesBox.Text) & 0xFFFF;
                    int y1 = (Convert.ToInt32(TimesBox.Text) >> 16) & 0xFFFF;
                    M.ModbusTCP.ModbusWrite(1, 16, 2002, new int[] { x1, y1 });
                    M.ModbusTCP.ModbusWrite(1, 16, 2102, new int[] { Convert.ToInt32(TimeBox.Text) });
                    M.ModbusTCP.ModbusWrite(1, 16, 501, new int[] { Convert.ToInt32(PreBox.Text) });
                    M.ModbusTCP.ModbusWrite(1, 16, 503, new int[] { Convert.ToInt32(SpeedBox.Text) });
                    int x = Convert.ToInt32(TimesBox1.Text) & 0xFFFF;
                    int y = (Convert.ToInt32(TimesBox1.Text) >> 16) & 0xFFFF;
                    M.ModbusTCP.ModbusWrite(1, 16, 2004, new int[] { x, y });
                    MessageBox.Show("设置成功");
                }
                else
                {
                    MessageBox.Show(mes);
                }
            }
            catch (Exception exception)
            {
                M.LdrLog(exception.Message);
                MessageBox.Show("设置失败");
            }
        }
    }
}
