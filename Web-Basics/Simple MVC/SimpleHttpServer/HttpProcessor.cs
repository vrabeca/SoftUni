﻿using SimpleHttpServer.Enums;
using SimpleHttpServer.Models;
using SimpleHttpServer.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SimpleHttpServer
{
    public class HttpProcessor
    {
        private IList<Route> Routes;
        private IDictionary<string,HttpSession> Sessions;
        private HttpRequest Request;
        private HttpResponse Response;

        public HttpProcessor(IEnumerable<Route> routes, IDictionary<string, HttpSession> Sessions)
        {
            this.Routes = new List<Route>(routes);
            this.Sessions = new Dictionary<string, HttpSession>();
        }

        public void HandleClient(TcpClient tcpClient)
        {
            using (Stream stream = tcpClient.GetStream())
            {
                Request = GetRequest(stream);
                Response = RouteRequest();
                Console.WriteLine("-RESPONSE-------------");
                Console.WriteLine(Response.Header);
                //Console.WriteLine(Encoding.UTF8.GetString(Response.Content));
                Console.WriteLine("----------------------");
                StreamUtils.WriteResponse(stream, Response);
            }
        }


        private HttpRequest GetRequest(Stream inputStream)
        {
            //Read Request Line
            string requestLine = StreamUtils.ReadLine(inputStream);
            string[] tokens = requestLine.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            RequestMethod method = (RequestMethod)Enum.Parse(typeof(RequestMethod), tokens[0].ToUpper());
            string url = tokens[1];
            string protocolVersion = tokens[2];

            //Read Headers
            Header header = new Header(HeaderType.HttpRequest);
            string line;
            while ((line = StreamUtils.ReadLine(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    break;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                string name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++;
                }

                string value = line.Substring(pos, line.Length - pos);
                if (name == "Cookie")
                {
                    string[] cookieSaves = value.Split(';');
                    foreach (var cookieSave in cookieSaves)
                    {
                        string[] cookiePair = cookieSave.Split('=').Select(x => x.Trim()).ToArray();
                        var cookie = new Cookie(cookiePair[0], cookiePair[1]);
                        header.AddCookie(cookie);
                    }
                }
                else if (name == "Location")
                {
                    header.Location = value;
                }
                else if (name == "Content-Length")
                {
                    header.ContentLength = value;
                }
                else
                {
                    header.OtherParameters.Add(name, value);
                }
            }

            string content = null;
            if (header.ContentLength != null)
            {
                int totalBytes = Convert.ToInt32(header.ContentLength);
                int bytesLeft = totalBytes;
                byte[] bytes = new byte[totalBytes];

                while (bytesLeft > 0)
                {
                    byte[] buffer = new byte[bytesLeft > 1024 ? 1024 : bytesLeft];
                    int n = inputStream.Read(buffer, 0, buffer.Length);
                    buffer.CopyTo(bytes, totalBytes - bytesLeft);

                    bytesLeft -= n;
                }

                content = Encoding.ASCII.GetString(bytes);
            }

            var request = new HttpRequest()
            {
                Method = method,
                Url = url,
                Header = header,
                Content = content
            };
            if (request.Header.Cookies.Contains("sessionId"))
            {
                var sessionId = request.Header.Cookies["sessionId"].Value;
                request.Session = new HttpSession(sessionId);
                if (!this.Sessions.ContainsKey(sessionId))
                {
                    this.Sessions.Add(sessionId, request.Session);
                }
            }
            
            Console.WriteLine("-REQUEST-----------------------------");
            Console.WriteLine(request);
            Console.WriteLine("------------------------------");

            return request;
        }
        private HttpResponse RouteRequest()
        {
            var routes = this.Routes
                .Where(x => Regex.Match(Request.Url, x.UrlRegex).Success)
                .ToList();

            if (!routes.Any())
                return HttpResponseBuilder.NotFound();

            var route = routes.SingleOrDefault(x => x.Method == Request.Method);

            if (route == null)
                return new HttpResponse()
                {
                    StatusCode = ResponseStatusCode.MethodNotAllowed
                };

            if (Request.Session == null)
            {
                var session = SessionCreator.Create();
                Request.Session = session;
            }

            var response = route.Callable(Request);
            if (!Request.Header.Cookies.Contains("sesionId"))
            {
                var sessionCookie = new Cookie("sessionId", this.Request.Session.Id + "; HttpOnly; path=/");
                response.Header.AddCookie(sessionCookie);
            }
            return response;

        }
    }
}
