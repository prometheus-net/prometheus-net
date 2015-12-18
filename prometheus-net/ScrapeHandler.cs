using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Prometheus.Internal;

namespace Prometheus
{
    internal class ScrapeHandler
    {
        private const string ProtoContentType = "application/vnd.google.protobuf; proto=io.prometheus.client.MetricFamily; encoding=delimited";
        private const string TextContentType = "text/plain; version=0.0.4";
        private const string ProtoAcceptType = "application/vnd.google.protobuf";

        public void ProcessScrapeRequest(
            IEnumerable<Advanced.DataContracts.MetricFamily> collected,
            string contentType,
            Stream outputStream)
        {
            if (contentType == ProtoContentType)
            {
                ProtoFormatter.Format(outputStream, collected);
            }
            else
            {
                AsciiFormatter.Format(outputStream, collected);
            }
        }

        public static string GetContentType(IEnumerable<string> acceptHeaders)
        {
            return ProtobufAccepted(acceptHeaders) ? ProtoContentType : TextContentType;
        }

        private static bool ProtobufAccepted(IEnumerable<string> acceptTypesHeader)
        {
            if (acceptTypesHeader == null)
                return false;

            var splitParams = acceptTypesHeader.Select(_ => _.Split(';'));
            var acceptTypes = splitParams.Select(_ => _.First()).ToList();

            return acceptTypes.Any(_ => _.Equals(ProtoAcceptType, StringComparison.OrdinalIgnoreCase));
        }
    }
}
