using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Threading;

namespace RAS
{
    public class FileHelper
    {

        private FileStream File_fs;
        private StreamWriter File_sw;

        private string FilePath = @"";
        private string FolderPath = @"";

        private bool IsSetFilePath = false;

        private bool IsEnableSave = false;

        public struct FileHeader
        {
            public string Title;   /**保存文件标题**/
            public string Item;    /**事项**/
            public string Tester;  /**测试者**/
            public string StartData;   /**开始保存时间**/
            public string Remarks; /**备注**/
        };

        public struct FileButtom
        {
            public float Offset_Fx;    /**偏置**/
            public float Offset_Fy;    /**偏置**/
            public float Offset_Fz;    /**偏置**/
            public float Offset_Tx;    /**偏置**/
            public float Offset_Ty;    /**偏置**/
            public float Offset_Tz;    /**偏置**/
            public string EndData; /**结束保存时间**/
            public string Remarks; /**备注**/
        };

        public FileHeader MyHeader;
        public FileButtom MyButtom;


        public FileHelper()
        {
            MyHeader.Title = "";
            MyHeader.Item = "";
            MyHeader.Remarks = "";
            MyHeader.StartData = "";
            MyHeader.Tester = "";
            MyButtom.Remarks = "";
        }


        /// <summary>
        /// 配置表头
        /// </summary>
        /// <param name="Title"></param>
        /// <param name="Item"></param>
        /// <param name="Tester"></param>
        /// <param name="Remarks"></param>
        /// <param name="StartData"></param>
        public void SetFileHeader(string Title, string Item, string Tester, string Remarks, string StartData = "")
        {
            MyHeader.Title = Title;
            MyHeader.Item = Item;
            MyHeader.Remarks = Remarks;
            MyHeader.StartData = StartData;
            MyHeader.Tester = Tester;
        }
        /// <summary>
        /// 配置偏置和表底备注
        /// </summary>
        /// <param name="Offset">偏置数组</param>
        /// <param name="Remarks">表底备注</param>
        public void SetOffset(float[] Offset, string Remarks = "")
        {
            MyButtom.Offset_Fx = Offset[0];
            MyButtom.Offset_Fy = Offset[1];
            MyButtom.Offset_Fz = Offset[2];
            MyButtom.Offset_Tx = Offset[3];
            MyButtom.Offset_Ty = Offset[4];
            MyButtom.Offset_Tz = Offset[5];
            MyButtom.Remarks = Remarks;
        }




        /// <summary>
        /// 设置保存文件的路径，如果路径不存在将会被创建
        /// </summary>
        /// <param name="path">文件路径，注意此处不是文件夹路径</param>
        public void SetFilePath(string path)
        {
            FilePath = @"" + path;
            FolderPath = @"";
            var b = FilePath.Split('\\');
            for (int i = 0; i < b.Length - 1; i++)
            {
                FolderPath = FolderPath + b[i] + @"\";
            }
            try
            {
                if (!Directory.Exists(FolderPath))
                {
                    Directory.CreateDirectory(FolderPath);
                }
                IsSetFilePath = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// 弹出对话框，设置保存文件的路径，如果路径不存在将会被创建
        /// </summary>
        public void SetFilePath()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            if (!Directory.Exists(Application.StartupPath))
            {
                Directory.CreateDirectory(Application.StartupPath);
            }
            saveFileDialog.InitialDirectory = Application.StartupPath;
            saveFileDialog.Title = "选择要保存的文件路径";
            saveFileDialog.Filter = "文本文件（*.txt）|*.txt|excel97-2003(*.xls)|*.xls";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                FilePath = Path.GetFullPath(saveFileDialog.FileName);
                IsSetFilePath = true;
            }
        }
        /// <summary>
        /// 获取保存文件的文件夹目录
        /// </summary>
        /// <returns>文件夹目录</returns>
        private string GetFolderPath()
        {
            if (IsSetFilePath)
            {
                FolderPath = @"";
                var b = FilePath.Split('\\');
                for (int i = 0; i < b.Length - 1; i++)
                {
                    FolderPath = FolderPath + b[i] + @"\";
                }
                return FolderPath;
            }
            else
            {
                MessageBox.Show("无法获取到文件路径，请重新设置文件路径！", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }
        }
        /// <summary>
        /// 获取获取保存文件的文件夹目录
        /// </summary>
        /// <returns>文件夹目录</returns>
        public string GetFilePath()
        {
            return GetFolderPath();
        }
        /// <summary>
        /// 使用资源管理器打开保存文件的文件夹目录
        /// </summary>
        public void OpenFolderPath()
        {
            if (IsSetFilePath)
            {
                System.Diagnostics.Process.Start("Explorer.exe", FolderPath);
            }
            else
            {
                MessageBox.Show("无法实现打开操作，请重新设置文件路径！", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }





        /// <summary>
        /// 开始保存操作
        /// </summary>
        public void StratSaveFile()
        {
            if (!IsSetFilePath)
            {
                MessageBox.Show("无法实现保存操作，请重新设置文件路径！", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (IsEnableSave)
            {
                MessageBox.Show("已经处于保存状态，请重新开始保存操作！", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            File_fs = new FileStream(FilePath, FileMode.OpenOrCreate);
            File_sw = new StreamWriter(File_fs);

            IsEnableSave = true;
        }
        /// <summary>
        /// 获取当前存储状态
        /// </summary>
        /// <returns>是否正在保存</returns>
        public bool GetSaveState()
        {
            return IsEnableSave;
        }





        /// <summary>
        /// 保存表头
        /// </summary>
        public void SaveHeader()
        {
            if (!IsEnableSave)
            {
                Console.WriteLine("[ERROE]--当前未处于保存状态，请配置存储状态。");
                return;
            }
            File_sw.WriteLine(MyHeader.Title);
            File_sw.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            File_sw.WriteLine("┃  PATIENT:" + MyHeader.Tester + "\t" + "┃  ITEM:" + MyHeader.Item + "\t" + "┃  STRAT TIME:" + DateTime.Now.ToString() + "\t" + "┃  Remarks:" + MyHeader.Remarks);
            File_sw.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        /// <summary>
        /// 保存表头
        /// </summary>
        /// <param name="dimension">30:三维角度表头；60:六维力/矩表头；31:三维角度+理想角度表头</param>
        public void SaveHeader(int dimension)
        {
            if (!IsEnableSave)
            {
                Console.WriteLine("[ERROE]--当前未处于保存状态，请配置存储状态。");
                return;
            }
            File_sw.WriteLine(MyHeader.Title);
            File_sw.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            File_sw.WriteLine("┃  PATIENT:" + MyHeader.Tester + "\t" + "┃  ITEM:" + MyHeader.Item + "\t" + "┃  STRAT TIME:" + DateTime.Now.ToString() + "\t" + "┃  Remarks:" + MyHeader.Remarks);
            File_sw.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (dimension == 30)
            {
                File_sw.WriteLine("X" + "\t" + "Y" + "\t" + "Z");
            }
            else if (dimension == 31)
            {
                File_sw.WriteLine("X" + "\t" + "Y" + "\t" + "Z" + "\t" + "Xi" + "\t" + "Yi" + "\t" + "Zi");
            }
            else if (dimension == 60)
            {
                File_sw.WriteLine("Fx" + "\t" + "Tx" + "\t" + "Fy" + "\t" + "Ty" + "\t" + "Fz" + "\t" + "Tz");
            }
            else
            { return; }
        }









        /// <summary>
        /// 存储一条数据到文档
        /// </summary>
        /// <param name="str"></param>
        public void SaveOneItem(string str)
        {
            if (IsEnableSave)
            {
                File_sw.WriteLine(str);
            }
            else
            {
                Console.WriteLine("[ERROE]--当前未处于保存状态，请配置存储状态。");
            }

        }
        /// <summary>
        /// 存储一条三维角度坐标数据到文档
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="z">Z坐标</param>
        public void SaveOneItem(float x, float y, float z)
        {
            if (IsEnableSave)
            {
                File_sw.WriteLine(x.ToString("0.0000") + "\t" + y.ToString("0.0000") + "\t" + z.ToString("0.0000"));
            }
            else
            {
                Console.WriteLine("[ERROE]--当前未处于保存状态，请配置存储状态。");
            }
        }
        /// <summary>
        /// 存储一条六维力/矩坐标数据到文档
        /// </summary>
        /// <param name="Fx"></param>
        /// <param name="Fy"></param>
        /// <param name="Fz"></param>
        /// <param name="Tx"></param>
        /// <param name="Ty"></param>
        /// <param name="Tz"></param>
        public void SaveOneItem(float Fx, float Fy, float Fz, float Tx, float Ty, float Tz)
        {
            if (IsEnableSave)
            {
                File_sw.WriteLine(Fx.ToString("0.0000") + "\t" + Fy.ToString("0.0000") + "\t" + Fz.ToString("0.0000") + "\t" + Tx.ToString("0.0000") + "\t" + Ty.ToString("0.0000") + "\t" + Tz.ToString("0.0000"));
            }
            else
            {
                Console.WriteLine("[ERROE]--当前未处于保存状态，请配置存储状态。");
            }
        }










        /// <summary>
        /// 废弃：保存数据表底
        /// </summary>
        public void SaveBottom()
        {
            if (!IsEnableSave)
            {
                Console.WriteLine("[ERROE]--当前未处于保存状态，请配置存储状态。");
                return;
            }
            File_sw.WriteLine("─────────────────────────────────────────────────────────────────────────────────────");
            File_sw.WriteLine("END Time:" + DateTime.Now.ToString());
            File_sw.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            if (MyButtom.Remarks != "") File_sw.WriteLine("Note:" + MyButtom.Remarks + ".");
            IsEnableSave = false;
            File_sw.Flush();
            File_sw.Close();
        }
        /// <summary>
        /// 废弃：保存数据表底
        /// </summary>
        /// <param name="IsOffset">是否保存偏置</param>
        public void SaveBottom(bool IsOffset = false)
        {
            if (!IsOffset)
            {
                SaveBottom();
            }
            else
            {
                if (!IsEnableSave)
                {
                    Console.WriteLine("[ERROE]--当前未处于保存状态，请配置存储状态。");
                    return;
                }
                File_sw.WriteLine("─────────────────────────────────────────────────────────────────────────────────────");
                File_sw.WriteLine("Offset_Fx：" + MyButtom.Offset_Fx.ToString("0.000") + "\t" +
                                "Offset_Fy：" + MyButtom.Offset_Fy.ToString("0.000") + "\t" +
                                "Offset_Fz：" + MyButtom.Offset_Fz.ToString("0.000") + "\t" +
                                "Offset_Tx：" + MyButtom.Offset_Tx.ToString("0.000") + "\t" +
                                "Offset_Ty：" + MyButtom.Offset_Ty.ToString("0.000") + "\t" +
                                "Offset_Tz：" + MyButtom.Offset_Tz.ToString("0.000"));
                File_sw.WriteLine("END Time:" + DateTime.Now.ToString());
                File_sw.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                if (MyButtom.Remarks != "") File_sw.WriteLine("Note:" + MyButtom.Remarks + ".");
                IsEnableSave = false;
                File_sw.Flush();
                File_sw.Close();
            }

        }



        /// <summary>
        /// 结束文件存储
        /// </summary>
        public void ForcedStop()
        {
            IsEnableSave = false;
            try
            {
                File_sw.Flush();
                File_sw.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class FileHelperOnForm//废弃
    {
        FileHelper AngleFile;
        FileHelper FTFile;

        int Timesample2SaveFIle = 100;//ms
        bool IsSaveFile = false;
        


        /// <summary>
        /// 开始保存数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button26_Click(object sender, EventArgs e)
        {
            AngleFile = new FileHelper();
            FTFile = new FileHelper();

            string filePath = Application.StartupPath + @"\Data\" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "_Angle.txt";
            AngleFile.SetFilePath(filePath);
            filePath = Application.StartupPath + @"\Data\" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "_FT.txt";
            FTFile.SetFilePath(filePath);

            AngleFile.SetFileHeader("","","","");
            FTFile.SetFileHeader("", "", "", "");

            AngleFile.StratSaveFile();
            FTFile.StratSaveFile();

            IsSaveFile = true;

            Thread childThread = new Thread(Save_main);
            childThread.Start();

            //button26.Enabled = false;
            //button27.Enabled = true;
        }


        /// <summary>
        /// 停止保存数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button27_Click(object sender, EventArgs e)
        {
            IsSaveFile = false;

            AngleFile.SaveBottom();
            FTFile.SaveBottom();

            //button27.Enabled = false;
            //button26.Enabled = true;
        }
        /// <summary>
        /// 打开保存目录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button28_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("Explorer.exe", Application.StartupPath + @"\Data");
        }

        /// <summary>
        /// 保存一条数据
        /// </summary>
        private void AddItem2File()
        {
            AngleFile.SaveOneItem(0, 0, 0, 0, 0, 0);
            FTFile.SaveOneItem(0, 0, 0, 0, 0, 0);
        }
        /// <summary>
        /// 保存线程
        /// </summary>
        private void Save_main()
        {
            while (IsSaveFile)
            {
                Thread childThread = new Thread(AddItem2File);
                childThread.Start();
                Thread.Sleep(Timesample2SaveFIle);
            }
            if (!IsSaveFile)
            {
                Thread.Sleep(Timesample2SaveFIle);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }
    }
}
