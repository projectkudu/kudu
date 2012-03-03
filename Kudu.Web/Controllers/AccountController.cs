using System;
using System.Web;
using System.Web.Mvc;
using Kudu.Web.Models;
using Kudu.Web.Services;

namespace Kudu.Web.Controllers
{
    public class AccountController : Controller
    {
        public IWebSecurityService WebSecurityService { get; set; }
        public IMessengerService MessengerService { get; set; }

        public AccountController(IWebSecurityService webSecurityService, IMessengerService messengerService)
        {
            WebSecurityService = webSecurityService;
            MessengerService = messengerService;
        }

        // **************************************
        // URL: /Account/LogOn
        // **************************************

        public ActionResult LogOn()
        {
            return View();
        }

        public ActionResult Login()
        {
            return View("LogOn");
        }

        [HttpPost]
        public ActionResult LogOn(LogOnModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                if (WebSecurityService.Login(model.UserName, model.Password, model.RememberMe))
                {
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        return RedirectToAction("Index", "Application");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "The user name or password provided is incorrect.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        // **************************************
        // URL: /Account/LogOff
        // **************************************

        public ActionResult LogOff()
        {
            WebSecurityService.Logout();

            return RedirectToAction("Index", "Application");
        }

        // **************************************
        // URL: /Account/Register
        // **************************************

        public ActionResult Register()
        {
            ViewBag.PasswordLength = WebSecurityService.MinPasswordLength;
            return View();
        }

        [HttpPost]
        public ActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                // Attempt to register the user
                var requireEmailConfirmation = false;
                var token = WebSecurityService.CreateUserAndAccount(model.UserName, model.Password, requireConfirmationToken: requireEmailConfirmation);

                if (requireEmailConfirmation)
                {
                    // Send email to user with confirmation token
                    string hostUrl = Request.Url.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
                    string confirmationUrl = hostUrl + VirtualPathUtility.ToAbsolute("~/Account/Confirm?confirmationCode=" + HttpUtility.UrlEncode(token));
                    
                    var fromAddress = "Your Email Address";
                    var toAddress = model.Email;
                    var subject = "Thanks for registering but first you need to confirm your registration...";
                    var body = string.Format("Your confirmation code is: {0}. Visit <a href=\"{1}\">{1}</a> to activate your account.", token, confirmationUrl);
                    
                    // NOTE: This is just for sample purposes
                    // It's generally a best practice to not send emails (or do anything on that could take a long time and potentially fail)
                    // on the same thread as the main site
                    // You should probably hand this off to a background MessageSender service by queueing the email, etc.
                    MessengerService.Send(fromAddress, toAddress, subject, body, true);
                    
                    // Thank the user for registering and let them know an email is on its way
                    return RedirectToAction("Thanks", "Account");
                }
                else
                {
                    // Navigate back to the homepage and exit
                    WebSecurityService.Login(model.UserName, model.Password);
                    return RedirectToAction("Index", "Application");
                }
            }

            // If we got this far, something failed, redisplay form
            ViewBag.PasswordLength = WebSecurityService.MinPasswordLength;
            return View(model);
        }
        
        public ActionResult Confirm()
        {
            string confirmationToken = Request.QueryString["confirmationCode"];
            WebSecurityService.Logout();

            if (!string.IsNullOrEmpty(confirmationToken)) 
            {
                if (WebSecurityService.ConfirmAccount(confirmationToken)) 
                {
                    ViewBag.Message = "Registration Confirmed! Click on the login link at the top right of the page to continue.";
                } else {
                    ViewBag.Message = "Could not confirm your registration info";
                }
            }

            return View();
        }

        // **************************************
        // URL: /Account/ChangePassword
        // **************************************

        [Authorize]
        public ActionResult ChangePassword()
        {
            ViewBag.PasswordLength = WebSecurityService.MinPasswordLength;
            return View();
        }

        [Authorize]
        [HttpPost]
        public ActionResult ChangePassword(ChangePasswordModel model)
        {
            if (ModelState.IsValid)
            {
                if (WebSecurityService.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword))
                {
                    return RedirectToAction("ChangePasswordSuccess");
                }
                else
                {
                    ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
                }
            }

            // If we got this far, something failed, redisplay form
            ViewBag.PasswordLength = WebSecurityService.MinPasswordLength;
            return View(model);
        }
        
        // **************************************
        // URL: /Account/ChangePasswordSuccess
        // **************************************

        public ActionResult ChangePasswordSuccess()
        {
            return View();
        }

        public ActionResult Thanks()
        {
            return View();
        }

    }
}
