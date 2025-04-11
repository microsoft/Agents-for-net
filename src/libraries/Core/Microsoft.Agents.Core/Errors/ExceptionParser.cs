// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Text;

namespace Microsoft.Agents.Core.Errors
{
    public static class ExceptionParser
    {
        public static void GetExceptionDetail(this Exception ex, StringBuilder sw, int level, StringBuilder? lastErrorMsg = null)
        {
            Exception generalEx = ex;
            bool firstIteration = false;
            if (level == 0)
            {
                sw.AppendLine();
                firstIteration = true;
                lastErrorMsg ??= new StringBuilder(1024);
            }

            FormatExceptionMessage(
                generalEx.Source != null ? generalEx.Source.ToString().Trim() : "Not Provided",
                generalEx.TargetSite != null ? generalEx.TargetSite.Name.ToString() : "Not Provided",
                string.IsNullOrEmpty(generalEx.Message) ? "Not Provided" : generalEx.Message.ToString().Trim(),
                string.IsNullOrEmpty(generalEx.HelpLink) ? "Not Provided" : generalEx.HelpLink.ToString().Trim(),
                generalEx.HResult == 0 ? null : generalEx.HResult,
                string.IsNullOrEmpty(generalEx.StackTrace) ? "Not Provided" : generalEx.StackTrace.ToString().Trim()
                , sw, level);

            lastErrorMsg.Append(string.IsNullOrEmpty(generalEx.Message) ? "Not Provided" : generalEx.Message.ToString().Trim());

            if (lastErrorMsg.Length > 0 && generalEx.InnerException != null)
                lastErrorMsg.Append(" => ");

            level++;
            if (generalEx.InnerException != null)
                GetExceptionDetail(generalEx.InnerException, sw, level, lastErrorMsg);

            if (firstIteration)
                sw.Insert(0, lastErrorMsg);
        }

        /// <summary>
        /// Creates the exception message.
        /// </summary>
        /// <param name="source">Source of Exception</param>
        /// <param name="targetSite">Target of Exception</param>
        /// <param name="message">Exception Message</param>
        /// <param name="stackTrace">StackTrace</param>
        /// <param name="helpLink">Url for help. </param>
        /// <param name="errorCode">Error Code</param>
        /// <param name="sw">Writer to write too</param>
        /// <param name="level">Depth of Exception</param>
        private static void FormatExceptionMessage(string source, string targetSite, string message, string helpLink, int? errorCode, string stackTrace, StringBuilder sw, int level)
        {
            if (level != 0)
                sw.AppendLine($"Inner Exception Level {level}\t: ");
            if (errorCode.HasValue)
            {
                if (level == 0)
                    sw.AppendLine("=====================================");
                sw.AppendLine("ErrorCode: " + errorCode.Value);
            }
            sw.AppendLine("Source: " + source);
            sw.AppendLine("Method: " + targetSite);
            sw.AppendLine("TimeStamp: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sw.AppendLine("Error: " + message);
            sw.AppendLine($"HelpLink Url: {helpLink}");
            if (!string.IsNullOrEmpty(stackTrace))
                sw.AppendLine("Stack Trace: " + stackTrace);
            sw.AppendLine("=====================================");
        }
    }
}
