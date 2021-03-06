﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace DataDepersonalizer
{
	public partial class MainForm : Form
	{

		bool isInProgress;
		int startFrom;

		public MainForm()
		{
			InitializeComponent();
		}

		private string AddTrailingBackSlash(string path)
		{
			if (!string.IsNullOrEmpty(path) && (path[path.Length - 1] != Path.DirectorySeparatorChar) && (path[path.Length - 1] != Path.AltDirectorySeparatorChar))
			{
				return path + Path.DirectorySeparatorChar;
			}
			return path;
		}

		private void PutLogMessage(string message)
		{
			txtLog.Text += message + "\r\n";
			txtLog.Select(txtLog.Text.Length, 0);
			txtLog.ScrollToCaret();
		}

		private string[] ExtractData(string text, string matchPattern)
		{
			var regex = new Regex(matchPattern, RegexOptions.IgnoreCase);

			var matches = regex.Matches(text);

			var list = new List<string>();

			foreach (Match match in matches)
			{
				var data = match.Value;

				if (list.IndexOf(data) < 0)
				{
					list.Add(data);
				}
			}

			return list.ToArray();
		}

		private string[] ExtractEmails(string text)
		{
			const string matchPattern =
				@"(([\w-]+\.)+[\w-]+|([a-zA-Z]{1}|[\w-]{2,}))@" +
				@"((([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\." +
				@"([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])){1}|" +
				@"([a-zA-Z]+[\w-]+\.)+[a-zA-Z]{2,4})";

			return ExtractData(text, matchPattern);
		}

		private string[] ExtractIpAddresses(string text)
		{
			const string matchPattern = @"([0-9]{1,3}[\.]){3}[0-9]{1,3}";

			return ExtractData(text, matchPattern);
		}

		private string GetEncodedEmail(string email)
		{
			var bytes = Encoding.Default.GetBytes(email);
			var base64 = Convert.ToBase64String(bytes);
			return Uri.EscapeDataString(base64);
		}

		private string ReplaceEmails(string msgSource)
		{
			var emails = ExtractEmails(msgSource);

			foreach (var email in emails)
			{
				var depersonalizedEmail = String.Format(txtReplaceMask.Text, startFrom++);
				msgSource = msgSource.Replace(email, depersonalizedEmail);

				var encodedEmail = GetEncodedEmail(email);
				msgSource = msgSource.Replace(encodedEmail, "");
			}

			return msgSource;
		}

		private int NormalizeIpPart(int part)
		{
			if (part > -1 && part < 256)
			{
				return part;
			}
			return part % 100;
		}

		private string ReplaceIpAddresses(string msgSource)
		{
			var ipAddresses = ExtractIpAddresses(msgSource);

			foreach (var ip in ipAddresses)
			{
				msgSource = msgSource.Replace(ip, String.Format(txtReplaceIpAddr.Text, NormalizeIpPart(startFrom++)));
			}

			return msgSource;
		}

		private string[] ExtractXmlNodes(string nodeName, string text)
		{
			string matchPattern = @"<" + nodeName + ">(.*?)</" + nodeName + ">";

			return ExtractData(text, matchPattern);
		}

		private string ReplaceXmlNode(string nodeName, string replaceWithMask, string msgSource)
		{
			var xmlNodes = ExtractXmlNodes(nodeName, msgSource);

			foreach (var node in xmlNodes)
			{
				var replaceWith = String.Format(replaceWithMask, startFrom++);
				msgSource = msgSource.Replace(node, String.Format("<{0}>{1}</{0}>", nodeName, replaceWith));
			}

			return msgSource;
		}

		private string ReplaceXmlNodes(string msgSource)
		{
			if (txtXmlNodes.Lines == null || txtReplaceWith.Lines == null)
			{
				return msgSource;
			}

			if (txtXmlNodes.Lines.Length != txtReplaceWith.Lines.Length)
			{
				throw new Exception("The number of XML nodes must be the same as Replace With values");
			}

			for (int i = 0; i < txtXmlNodes.Lines.Length; i++)
			{
				msgSource = ReplaceXmlNode(txtXmlNodes.Lines[i], txtReplaceWith.Lines[i], msgSource);
			}

			return msgSource;
		}

		private void btnOpenEmailFolder_Click(object sender, EventArgs e)
		{
			if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
			{
				txtEmailFolder.Text = folderBrowserDialog1.SelectedPath;
			}
		}

		private void btnDepersonalize_Click(object sender, EventArgs e)
		{
			if (isInProgress) return;

			isInProgress = true;
			try
			{
				PutLogMessage("Start data depersonalization...");

				startFrom = Convert.ToInt32(txtStartFrom.Text);

				var list = Directory.GetFileSystemEntries(AddTrailingBackSlash(txtEmailFolder.Text), "*.*");

				Encoding encoding;
				if (!cbWriteBom.Checked && (txtEncoding.Text.ToLower() == "utf-8"))
				{
					encoding = new UTF8Encoding(false);
				}
				else
				{
					encoding = Encoding.GetEncoding(txtEncoding.Text);
				}

				foreach (var fileEntry in list)
				{
					var msgSource = File.ReadAllText(fileEntry, encoding);

					msgSource = ReplaceEmails(msgSource);

					msgSource = ReplaceIpAddresses(msgSource);

					msgSource = ReplaceXmlNodes(msgSource);

					File.WriteAllText(fileEntry, msgSource, encoding);

					PutLogMessage(String.Format("Msg \"{0}\" depersonalized.", Path.GetFileName(fileEntry)));
				}

				PutLogMessage("E-mails replaced, IP addresses replaced, sensitive data removed.\r\nDone.");
			}
			finally
			{
				isInProgress = false;
				txtStartFrom.Text = startFrom.ToString();
			}
		}
	}
}
