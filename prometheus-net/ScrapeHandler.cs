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

        public string ProcessScrapeRequest(
            IEnumerable<Advanced.DataContracts.MetricFamily> collected, 
            IEnumerable<string> acceptHeaders, 
            Stream outputStream)
        {
            if (ProtobufAccepted(acceptHeaders))
            {
                ProtoFormatter.Format(outputStream, collected);
                return ProtoContentType;
            }
            else
            {
                AsciiFormatter.Format(outputStream, collected);
                return TextContentType;
            }
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
