using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Clipboard = System.Windows.Clipboard;

namespace MouseClickVoice
{
    public class TextSimulator
    {
        private readonly double _typingDelay;

        public TextSimulator(double typingDelay = 0.05)
        {
            _typingDelay = typingDelay;
        }

        public async Task TypeTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                // 短暂延迟确保焦点正确
                await Task.Delay(100);

                // 使用SendKeys模拟键盘输入
                System.Windows.Forms.SendKeys.SendWait(text);
            }
            catch (Exception ex)
            {
                throw new Exception($"文本输入失败: {ex.Message}");
            }
        }

        public void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // 使用剪贴板方式插入文本
            try
            {
                var originalText = Clipboard.GetText();
                Clipboard.SetText(text);

                // 发送Ctrl+V
                System.Windows.Forms.SendKeys.SendWait("^v");

                // 恢复原始剪贴板内容
                if (!string.IsNullOrEmpty(originalText))
                {
                    Clipboard.SetText(originalText);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"剪贴板插入失败: {ex.Message}");
            }
        }
    }
}