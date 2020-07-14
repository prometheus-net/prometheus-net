//using System;
//using System.Diagnostics;

//namespace Prometheus.DiagnosticSource
//{
//    public class HttpClientSubscriber : IObserver<DiagnosticListener>
//    {
//        public void OnCompleted()
//        {
//        }

//        public void OnError(Exception error)
//        {
//        }

//        public void OnNext(DiagnosticListener value)
//        {
//            if (value.Name == "HttpHandlerDiagnosticListener")
//            {
//                value.Subscribe(new TestClassListener());
//            }

//            if (value.Name == "")
//            {
//                value.Subscribe(new TestClassListener());
//            }
//        }
//    }
//}