﻿using System.Linq;
using System.Web.Mvc;
using NGM.Forum.Extensions;
using NGM.Forum.Models;
using NGM.Forum.Services;
using Orchard;
using Orchard.ContentManagement;
using Orchard.DisplayManagement;
using Orchard.Localization;
using Orchard.Mvc;
using Orchard.Security;
using Orchard.Settings;
using Orchard.Themes;
using Orchard.UI.Navigation;
using Orchard.UI.Notify;
using System.Collections.Generic;
using Orchard.UI.Zones;

namespace NGM.Forum.Controllers {
    [Themed]
    [ValidateInput(false)]
    public class SubscriptionController : Controller
    {
        private readonly IOrchardServices _orchardServices;
        private readonly IForumService _forumService;
        private readonly IThreadService _threadService;
        private readonly IPostService _postService;
        private readonly ISiteService _siteService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuthenticationService _authenticationService;
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(IOrchardServices orchardServices,
            IForumService forumService,
            IThreadService threadService,
            IPostService postService,
            ISiteService siteService,
            IShapeFactory shapeFactory,
            IAuthorizationService authorizationService,
            IAuthenticationService authenticationService,
            ISubscriptionService subscriptionService
            )
        {
            _orchardServices = orchardServices;
            _forumService = forumService;
            _threadService = threadService;
            _postService = postService;
            _siteService = siteService;
            _subscriptionService = subscriptionService;
            _authorizationService = authorizationService;
            _authenticationService = authenticationService;

            T = NullLocalizer.Instance;
            Shape = shapeFactory;
        }

        dynamic Shape { get; set; }
        public Localizer T { get; set; }

        public ActionResult AddSubscription(int threadId)
        {
            if (!_orchardServices.WorkContext.HttpContext.User.Identity.IsAuthenticated)
                return new HttpUnauthorizedResult(T("You must be logged in to subscribe to a thread.").ToString());

            if (!_orchardServices.Authorizer.Authorize(Permissions.CanPostToForums, T("You do not have permissions to subscribe to a thread.")))
                return new HttpUnauthorizedResult();

            if ( _orchardServices.WorkContext.CurrentUser == null )
                return new HttpUnauthorizedResult();

            var userId = _orchardServices.WorkContext.CurrentUser.Id;

            _subscriptionService.AddSubscription(userId, threadId);

            _orchardServices.Notifier.Add(NotifyType.Information, T("You have been subscribed to the thread."));

            return Redirect(_orchardServices.WorkContext.HttpContext.Request.UrlReferrer.AbsoluteUri); ;
        }

        [HttpPost]
        public ActionResult ManageSubscription(string unsubscribe, string sendNotificationsByEmail)
        {
            if (!_orchardServices.WorkContext.HttpContext.User.Identity.IsAuthenticated)
                return new HttpUnauthorizedResult(T("You must be logged in to unsubscribe to a discussion").ToString());

            if (!_orchardServices.Authorizer.Authorize(Permissions.CanPostToForums, T("You do not have permissions to subscribe to a thread.")))
                return new HttpUnauthorizedResult();

            var userId = _orchardServices.WorkContext.CurrentUser.Id;

            

            if (!string.IsNullOrWhiteSpace(sendNotificationsByEmail))
            {
                string action = sendNotificationsByEmail.Split('-')[0].ToLowerInvariant();
                int threadId = int.Parse(sendNotificationsByEmail.Split('-')[1]);
                if ( action.Equals("add")){
                    _subscriptionService.SendNewPostNotificationByEmail(userId, threadId, true);
                } else if ( action.Equals("remove")){
                    _subscriptionService.SendNewPostNotificationByEmail(userId, threadId, false);
                } else {
                    _orchardServices.Notifier.Add(NotifyType.Error, T("Error: An unrecognized option was encountered.  Could not change the subscription's email option."));
                }
            }            
            else if (!string.IsNullOrWhiteSpace(unsubscribe))
            {
                int threadId = int.Parse(unsubscribe);
                _subscriptionService.DeleteSubscription(userId, threadId);

                _orchardServices.Notifier.Add(NotifyType.Information, T("You have been unsubscribed from the thread."));
            }
            else
            {
                _orchardServices.Notifier.Add(NotifyType.Error, T("Error: An unrecognized option was selected."));
            }
            return Redirect(_orchardServices.WorkContext.HttpContext.Request.UrlReferrer.AbsoluteUri); ;
        }


        public ActionResult ViewSubscriptions( int forumsHomeId)
        {

            if (!_orchardServices.WorkContext.HttpContext.User.Identity.IsAuthenticated)
                return new HttpUnauthorizedResult(T("You must be logged in to view your subscriptions.").ToString());

            if (!_orchardServices.Authorizer.Authorize(Permissions.CanPostToForums, T("You do not have permissions to view subscriptions.")))
                return new HttpUnauthorizedResult();
            ForumsHomePagePart forumsHomepagePart = null;
            var siteForums = _orchardServices.ContentManager.Query<ForumsHomePagePart>().List().ToList();
            
            if (forumsHomeId != 0)
            {
                forumsHomepagePart = siteForums.Where(forumsHomePart => forumsHomePart.Id == forumsHomeId).FirstOrDefault();
            } 
            
            var userId = _orchardServices.WorkContext.CurrentUser.Id;

            var threadSubscriptionList = _subscriptionService.GetSubscribedThreads(userId);
            var settings = _subscriptionService.GetSubscriptionSettings(userId);
            foreach (var thread in threadSubscriptionList)
            {
                thread.UserIsSubscribedByEmail = settings[thread.Id].EmailUpdates;
            }
            var threadDisplay = threadSubscriptionList.Select(b => _orchardServices.ContentManager.BuildDisplay(b, "Subscription")); ;

            var list = Shape.List();
            list.AddRange(threadDisplay);
            var menuShape = Shape.Parts_ForumMenu(ForumsHomePagePart: forumsHomepagePart, ShowRecent: false, ShowMarkAll: false, ReturnUrl: HttpContext.Request.Url.AbsoluteUri);
            dynamic viewModel = _orchardServices.New.ViewModel().ContentItems(list).ForumMenu(menuShape).SubscriptionSettings(settings).ForumsHomepagePart(forumsHomepagePart).SiteForumsList(siteForums);

            return View((object)viewModel);

        }

    }

       
}