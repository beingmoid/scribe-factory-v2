﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Scribe_Factory_Core.Common;
using Scribe_Factory_Core.CustomFilters;
using Scribe_Factory_Core.Repositories;
using Scribe_Factory_Core.Repositories.Interfaces;
using Scribe_Factory_Core.ViewModels;
using Nancy.Json;
using System.Collections;
using Newtonsoft.Json;

namespace Scribe_Factory_Core.Controllers
{
    [AuthorizationFilter]
    public class FunctionsController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration Configuration;
        IProjectManagementRepository projectManagementRepository;
        AWSHelper aws = null;
        string BucketName = "";
        public FunctionsController(ILogger<HomeController> logger, IConfiguration configuration, IProjectManagementRepository projectManagementRepository)
        {
            _logger = logger;
            Configuration = configuration;
            this.projectManagementRepository = projectManagementRepository;
            var Accesskey = Configuration["AWSPollyAccessKey"];
            var SecretKey = Configuration["AWSPollySecretKey"];
            BucketName = Configuration["S3BucketName"];
            aws = new AWSHelper(Accesskey, SecretKey);
        }

        public IActionResult TextToSpeech()
        {
            return View();
        }

        [HttpGet]
        public IActionResult getLanguagesAgainstGender(string gender)
        {
            var Voices = aws.GetLanguagesAgainstGender(gender);
            return new JsonResult(Voices) { StatusCode = 200 };
        }
        [HttpGet]
        public IActionResult getVoicesAgainstLangugae(string LanguageCode, string gender)
        {
            var Voices = aws.GetVoicesAgainstLanguage(LanguageCode, gender);
            return new JsonResult(Voices) { StatusCode = 200 };
        }
        [HttpPost]
        public IActionResult ConvertTextToSpeech(string text, string VoiceId, string projectName, string ProjectType)
        {
            try
            {
                //saving a new project
                var currentUser = SessionHelper.GetObject<UserLoginViewModel>(HttpContext.Session, "CurrentUser");
                var projectId = projectManagementRepository.SaveProject(projectName, ProjectType, currentUser.User.Id);
                //settting current working projectId
                SessionHelper.SetObject(HttpContext.Session, "CurrentProjectId", projectId);
                projectManagementRepository.SaveProjectFile(projectId + "_" + projectName + ".mp3", projectId);

                //get details of voice from voiceID
                var voice = StaticVoiceDetails.AllVoices.Where(x => x.Id == VoiceId).FirstOrDefault();
                aws.ConvertTextToSpeech(text, voice.LanguageCode, voice.Name, voice.Engine.FirstOrDefault(), BucketName, projectId + "_" + projectName + ".mp3");
                ViewBag.ProjectId = projectId;
                return new JsonResult(projectId);
            }
            catch
            {
                return new JsonResult(false);

            }
        }
        [HttpPost]
        public IActionResult PlayAudio(string text, string VoiceId, string projectName, string ProjectType)
        {
            try
            {
                //saving a new project
                var currentUser = SessionHelper.GetObject<UserLoginViewModel>(HttpContext.Session, "CurrentUser");


                //get details of voice from voiceID
                var voice = StaticVoiceDetails.AllVoices.Where(x => x.Id == VoiceId).FirstOrDefault();
                aws.ConvertTextToSpeech(text, voice.LanguageCode, voice.Name, voice.Engine.FirstOrDefault(), currentUser.User.Id + "temp.mp3");


                return new JsonResult("https://"+Request.Host.Value+"/" + currentUser.User.Id + "temp.mp3");
            }
            catch
            {
                return new JsonResult(false);

            }
        }
        public IActionResult TextToTextConversion()
        {
            var languages = projectManagementRepository.GetLanguagesFromDB();
            return View(languages);
        }



        [HttpPost]
        public IActionResult Translate(String text, string FromLanguage, string ToLanguage)
        {
            //TranslateText(text);
            // Set the language from/to in the url (or pass it into this function)
            string url = String.Format
            ("https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}",
             FromLanguage, ToLanguage, Uri.EscapeUriString(text));
            HttpClient httpClient = new HttpClient();
            string result = httpClient.GetStringAsync(url).Result;

            // Get all json data
            var jsonData = JsonConvert.DeserializeObject<dynamic>(result);

            // Extract just the first array element (This is the only data we are interested in)
            var translationItems = jsonData[0];

            // Translation Data
            string translation = "";

            // Loop through the collection extracting the translated objects
            foreach (object item in translationItems)
            {
                // Convert the item array to IEnumerable
                IEnumerable translationLineObject = item as IEnumerable;

                // Convert the IEnumerable translationLineObject to a IEnumerator
                IEnumerator translationLineString = translationLineObject.GetEnumerator();

                // Get first object in IEnumerator
                translationLineString.MoveNext();

                // Save its value (translated text)
                translation += string.Format(" {0}", Convert.ToString(translationLineString.Current));
            }

            // Remove first blank character
            if (translation.Length > 1) { translation = translation.Substring(1); };

            try
            {

                return Json(translation);
            }
            catch
            {
                return Json(false);
            }
        }
        [HttpPost]
        public IActionResult SaveTexttoTextProject(String text, string ProjectName)
        {

            var currentUser = SessionHelper.GetObject<UserLoginViewModel>(HttpContext.Session, "CurrentUser");
            var projectId = projectManagementRepository.SaveProject(ProjectName, "TextToText", currentUser.User.Id);
            //memory stream for textfile
            MemoryStream ms = new MemoryStream(UTF8Encoding.UTF8.GetBytes(text));
            var isSaved = aws.sendMyFileToS3(ms, BucketName, "", projectId + "_" + ProjectName + ".txt");
            projectManagementRepository.SaveProjectFile(projectId + "_" + ProjectName + ".txt", projectId);
            return Json(isSaved);
        }

        [HttpGet]
        public IActionResult SpeechTOText()
        {
            return View();
        }

        [HttpGet]
        public IActionResult SpeechTOSpeech()
        {
            return View();

        }
    }
}
