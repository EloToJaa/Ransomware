using System;
using System.Text;
using System.Windows.Forms;

using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Encrypt
{
    public partial class MainForm : Form
    {
        [DllImport("KERNEL32.DLL", EntryPoint = "RtlZeroMemory")]
        public static extern bool ZeroMemory(IntPtr Destination, int Length);
        private const int bufferSize = 1048576 * 1024;
        private const string ext = ".tmp";
        private int errors;

        public MainForm()
        {
            InitializeComponent();
        }

        private bool isDirectory(string fileName)
        {
            return File.GetAttributes(fileName).HasFlag(FileAttributes.Directory);
        }

        private void btnBrowseFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "All files|*.*" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = ofd.FileName;
                }
            }
        }

        private void FileEncrypt(string fileName, string password)
        {
            byte[] passwords = Encoding.UTF8.GetBytes(password);
            byte[] salt = new byte[32];

            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            AES.Padding = PaddingMode.PKCS7;
            var key = new Rfc2898DeriveBytes(password, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Mode = CipherMode.CFB;
            
            using (FileStream fsCrypt = new FileStream(fileName + ext, FileMode.Create))
            {
                fsCrypt.Write(salt, 0, salt.Length);

                using (CryptoStream cs = new CryptoStream(fsCrypt, AES.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open))
                    {
                        byte[] buffer = new byte[bufferSize];
                        int read;
                        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cs.Write(buffer, 0, read);
                        }
                    }
                }
            }

            File.Delete(fileName);
            File.Move(fileName + ext, fileName);
            progressBar.Value++;
        }

        private void Encrypt(string fileName, string password)
        {
            if (!isDirectory(fileName))
            {
                FileEncrypt(fileName, password);
                return;
            }

            string[] files = Directory.GetFiles(fileName);
            foreach(string file in files)
            {
                FileEncrypt(file, password);
            }

            string[] dirs = Directory.GetDirectories(fileName);
            foreach (string dir in dirs)
            {
                Encrypt(dir, password);
            }
        }

        private int CountFiles(string fileName)
        {
            if (!isDirectory(fileName))
            {
                return 1;
            }

            string[] files = Directory.GetFiles(fileName);
            int count = files.Length;

            string[] dirs = Directory.GetDirectories(fileName);
            foreach (string dir in dirs)
            {
                count += CountFiles(dir);
            }

            return count;
        }

        private void btnEncrypt_Click(object sender, EventArgs e)
        {
            GCHandle gCHandle = GCHandle.Alloc(txtPassword.Text, GCHandleType.Pinned);

            int numberOfFiles = CountFiles(txtFilePath.Text);
            progressBar.Maximum = numberOfFiles;
            progressBar.Value = 0;
            Encrypt(txtFilePath.Text, txtPassword.Text);

            ZeroMemory(gCHandle.AddrOfPinnedObject(), txtPassword.Text.Length * 2);
            gCHandle.Free();

            MessageBox.Show($"Zaszyfrowano {numberOfFiles} plików!");
        }

        private void FileDecrypt(string fileName, string password)
        {
            byte[] passwords = Encoding.UTF8.GetBytes(password);
            byte[] salt = new byte[32];
            
            using (FileStream fsCrypt = new FileStream(fileName, FileMode.Open))
            {
                fsCrypt.Read(salt, 0, salt.Length);
                RijndaelManaged AES = new RijndaelManaged();
                AES.KeySize = 256;
                AES.BlockSize = 128;
                AES.Padding = PaddingMode.PKCS7;
                var key = new Rfc2898DeriveBytes(password, salt, 50000);
                AES.Key = key.GetBytes(AES.KeySize / 8);
                AES.IV = key.GetBytes(AES.BlockSize / 8);
                AES.Mode = CipherMode.CFB;
                
                try
                {
                    using (CryptoStream cs = new CryptoStream(fsCrypt, AES.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        using (FileStream fso = new FileStream(fileName + ext, FileMode.Create))
                        {
                            
                            byte[] buffer = new byte[bufferSize];
                            int read;
                            
                            while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                fso.Write(buffer, 0, read);
                            }
                        }
                    }
                }
                catch (CryptographicException e)
                {
                    File.Delete(fileName + ext);
                    errors++;
                    progressBar.Value++;
                    return;
                }
            }

            File.Delete(fileName);
            File.Move(fileName + ext, fileName);
            progressBar.Value++;
        }

        private void Decrypt(string fileName, string password)
        {
            if (!isDirectory(fileName))
            {
                FileDecrypt(fileName, password);
                return;
            }

            string[] files = Directory.GetFiles(fileName);
            foreach (string file in files)
            {
                FileDecrypt(file, password);
            }

            string[] dirs = Directory.GetDirectories(fileName);
            foreach (string dir in dirs)
            {
                Decrypt(dir, password);
            }
        }

        private void btnDecrypt_Click(object sender, EventArgs e)
        {
            GCHandle gch = GCHandle.Alloc(txtPassword.Text, GCHandleType.Pinned);

            errors = 0;
            int numberOfFiles = CountFiles(txtFilePath.Text);
            progressBar.Maximum = numberOfFiles;
            progressBar.Value = 0;
            Decrypt(txtFilePath.Text, txtPassword.Text);

            ZeroMemory(gch.AddrOfPinnedObject(), txtPassword.Text.Length * 2);
            gch.Free();

            if (errors > 0)
            {
                MessageBox.Show($"Nie odszyfrowano {errors} z {numberOfFiles} plików! Możliwe powody to złe hasło lub próba odszyfrowania niezaszyfrowanych plików.", "Błąd odszyfrowania!");
            }
            else
            {
                MessageBox.Show($"Odszyfrowano {numberOfFiles} plików!");
            }
        }

        private void btnBrowseDirectory_Click(object sender, EventArgs e)
        {

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    txtFilePath.Text = fbd.SelectedPath;
                }
            }
        }
    }
}