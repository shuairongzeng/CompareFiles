using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace WinFormsApp4
{
    public partial class Form1 : Form
    {
        private BackgroundWorker _backgroundWorker;
        public Form1()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterScreen;

        }


        private async void button1_Click(object sender, EventArgs e)
        {
            //string fileAPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gaomeinan-vmware-vmx.exe");
            //string fileBPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "normol-vmware-vmx.exe");
            string fileAPath =txt_firstPath.Text;
            string fileBPath =txt_secondPath.Text;
            string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "differences.txt");
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            await Task.Run(async () =>
            {
                Action<int> updateProgressBarUI = new Action<int>(progressPercentage =>
                {
                    while (!progressBar1.IsHandleCreated)
                    {
                        //解决窗体关闭时出现“访问已释放句柄“的异常
                        if (progressBar1.Disposing || progressBar1.IsDisposed)
                            return;
                    }

                    //采用委托的方式更新UI界面进度条显示的值
                    if (progressBar1.InvokeRequired) //progressBar1委托请求
                    {
                        Invoke(new Action(() => { progressBar1.Value = progressPercentage; }));
                    }
                    else
                    {
                        progressBar1.Value = progressPercentage;
                    }
                });

                // await CompareAndHighlightDifferencesAsync(fileAPath, fileBPath, updateProgressBarUI);

                await CompareAndWriteDifferencesAsync(fileAPath, fileBPath, outputPath, progress =>
                {
                    Debug.WriteLine($"Progress: {progress}%");
                    updateProgressBarUI(progress);
                }, p2 =>
                {
                    while (!richTextBox1.IsHandleCreated)
                    {
                        //解决窗体关闭时出现“访问已释放句柄“的异常
                        if (richTextBox1.Disposing || richTextBox1.IsDisposed)
                            return;
                    }

                    //采用委托的方式更新UI界面进度条显示的值
                    if (richTextBox1.InvokeRequired) //progressBar1委托请求
                    {
                        Invoke(new Action(() => { richTextBox1.AppendText(p2 + Environment.NewLine); }));
                    }
                    else
                    {
                        richTextBox1.AppendText(p2 + Environment.NewLine);
                    }

                });
            });

            //    await CompareAndHighlightDifferencesAsync(fileAPath, fileBPath, progress);

            // await CompareAndHighlightDifferencesAsync(fileAPath, fileBPath);
        }

        const int BufferSize = 4096;
        private async Task CompareAndHighlightDifferencesAsync(string fileAPath, string fileBPath, Action<int> progress)
        {
            long totalBytes = new FileInfo(fileAPath).Length;
            long totalBytesRead = 0;

            using (FileStream streamA = File.OpenRead(fileAPath))
            using (FileStream streamB = File.OpenRead(fileBPath))
            {
                byte[] bufferA = new byte[BufferSize];
                byte[] bufferB = new byte[BufferSize];
                int bytesReadA, bytesReadB;

                while ((bytesReadA = await streamA.ReadAsync(bufferA, 0, bufferA.Length)) > 0)
                {
                    bytesReadB = await streamB.ReadAsync(bufferB, 0, bufferB.Length);

                    totalBytesRead += bytesReadA;

                    for (int i = 0; i < bytesReadA; i++)
                    {
                        string hexA = bufferA[i].ToString("X2");
                        string hexB = i < bytesReadB ? bufferB[i].ToString("X2") : "00";

                        AppendTextWithColor(hexA + " ", hexA != hexB ? Color.Yellow : richTextBox1.BackColor);
                    }


                    // 更新进度
                    int progressPercentage = (int)(totalBytesRead * 100 / totalBytes) * 100;
                    //int progressPercentage = (int)((double)totalBytesRead * 100 / totalBytes);
                    progress(progressPercentage);
                }

            }
        }

        private void AppendTextWithColor(string text, Color backColor)
        {
            Invoke(new Action(() =>
            {
                richTextBox1.SelectionStart = richTextBox1.TextLength;
                richTextBox1.SelectionLength = 0;

                richTextBox1.SelectionBackColor = backColor;
                richTextBox1.AppendText(text);
                richTextBox1.SelectionBackColor = richTextBox1.BackColor;
            }));
        }
        List<CompareModel> outCompareModels = new List<CompareModel>();
        async Task CompareAndWriteDifferencesAsync(string fileAPath, string fileBPath, string outputPath, Action<int> progressCallback, Action<string> callback)
        {

            byte[] bufferA = new byte[BufferSize];
            byte[] bufferB = new byte[BufferSize];

            long totalBytes = new FileInfo(fileAPath).Length;
            long totalBytesRead = 0;
            int lastReportedProgress = 0;
            List<CompareModel> compareModels = new List<CompareModel>();

            using (FileStream streamA = File.OpenRead(fileAPath))
            using (FileStream streamB = File.OpenRead(fileBPath))
            using (StreamWriter output = new StreamWriter(outputPath))
            {
                int bytesReadA, bytesReadB;

                while ((bytesReadA = await streamA.ReadAsync(bufferA, 0, BufferSize)) > 0)
                {
                    bytesReadB = await streamB.ReadAsync(bufferB, 0, BufferSize);

                    for (int i = 0; i < bytesReadA || i < bytesReadB; i++)
                    {
                        byte byteA = i < bytesReadA ? bufferA[i] : (byte)0;
                        byte byteB = i < bytesReadB ? bufferB[i] : (byte)0;

                        if (byteA != byteB)
                        {
                            int offset = (int)(totalBytesRead + i);
                            compareModels.Add(new CompareModel { Offset = offset, OffsetValue = $"{byteA:X2}" });
                            await output.WriteLineAsync($"Offset {offset:X}: {byteA:X2} != {byteB:X2}");
                        }
                    }

                    totalBytesRead += bytesReadA;

                    // 更新进度
                    int progress = (int)(totalBytesRead * 100 / totalBytes);
                    if (progress != lastReportedProgress)
                    {
                        progressCallback(progress);
                        lastReportedProgress = progress;
                    }
                }
            }

            //int kj = 0;
            //compareModels.ForEach(x =>
            //{
            //    kj++;
            //    if (x != null && !outCompareModels.Any(c => c.Offset == x.Offset))
            //    {
            //        Debug.WriteLine($"Offset {x.Offset:X} {AddOffset(x.Offset, compareModels)}");
            //        callback?.Invoke($"Offset {x.Offset:X} {AddOffset(x.Offset, compareModels)}");
            //    }
            //    progressCallback((int)(kj * 100 / compareModels.Count));
            //});


        }


        public string AddOffset(int offset, List<CompareModel> compareModels)
        {

            string valueString = compareModels.FirstOrDefault(c => c.Offset == offset).OffsetValue;
            int nextNode = offset + 1;

            CompareModel compareModel = compareModels.FirstOrDefault(c => c.Offset == nextNode);
            if (compareModel != null)
            {
                outCompareModels.Add(new CompareModel
                {
                    Offset = compareModel.Offset,
                    OffsetValue = compareModel.OffsetValue
                });
                return valueString += AddOffset(compareModel.Offset, compareModels);
            }
            return valueString;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            txt_firstPath.Text= Path.Combine("K:","vmx", "vmware-vmx12.5.9.exe");
            txt_secondPath.Text = Path.Combine("K:", "vmx", "vn12.5.9xdgvmware-vmx.exe");
        }
    }
}