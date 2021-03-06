﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using NetworkTeknikServis.BLL.Repository;
using NetworkTeknikServis.BLL.Services.Senders;
using NetworkTeknikServis.MODELS.Entities;
using NetworkTeknikServis.MODELS.Enums;
using NetworkTeknikServis.MODELS.IdentityModels;
using NetworkTeknikServis.MODELS.Models;
using NetworkTeknikServis.MODELS.ViewModels;
using static NetworkTeknikServis.BLL.Identity.MembershipTools;

namespace NetworkTeknikServis.WEB.UI.Controllers
{
    
    [Authorize(Roles ="Technician,Admin")]
    public class TechnicianController : Controller
    {
        // GET: Technician
        public async Task<ActionResult> Index()
        {
            var teknisyen= await NewUserStore().FindByIdAsync(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId());
            var data = new FaultRepo().GetAll(x => x.TechnicianId == teknisyen.Id && x.FaultState == FaultState.Completed).ToList();
            return View(data);
        }
        public ActionResult FaultTracking(string id)
        {
            var data = new FaultRepo().GetAll(x => x.TechnicianId == id && (x.FaultState == FaultState.Uncompleted || x.FaultState == FaultState.Pending)).ToList();
            return View(data);
        }

        public async Task<ActionResult> ConfirmFault(Guid id)
        {
            try
            {
                var fault = new FaultRepo().GetById(id);
                var customer = await NewUserStore().FindByIdAsync(fault.CustomerId);
                
                var technician = await NewUserStore().FindByIdAsync(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId());
                if (technician != null)
                {
                    fault.haveJob = true;
                    
                    new FaultRepo().Update(fault);
                    var Log = new FaultLog
                    {
                        TechnicianId = fault.TechnicianId,
                        CustomerId = fault.CustomerId,
                        Operation = $"Arıza {technician.Name} {technician.Surname} isimli teknisyene arızayı onayladı",
                        FaultId = fault.FaultID,
                        OperationDescription = fault.FaultDescription
                    };
                    new FaultLogRepo().Insert(Log);

                    string SiteUrl = Request.Url.Scheme + System.Uri.SchemeDelimiter + Request.Url.Host +
                                     (Request.Url.IsDefaultPort ? "" : ":" + Request.Url.Port);

                    var emailService = new EmailService();
                    var body = $"Merhaba <b>{customer.Name} {customer.Surname}</b><br>{technician.Name} {technician.Surname} isimli teknisyen 2 iş günü içerisinde gelecek";
                    await emailService.SendAsync(new IdentityMessage() { Body = body, Subject = "Teknisyen atandı" }, customer.Email);
                }

                TempData["Message"] = $"Teknisyen arızayı onaylanmıştır";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Model"] = new ErrorViewModel()
                {
                    Text = $"Bir hata oluştu {ex.Message}",
                    ActionName = "Index",
                    ControllerName = "Operator",
                    ErrorCode = 500
                };
                return RedirectToAction("Error", "Home");
            }
        }
        [HttpGet]
        public async Task<JsonResult> GetDetail(Guid id)
        {
            try
            {
                var fault = new FaultRepo().GetById(id);
                
                var faults = new FaultRepo().GetAll();
                var customer = await NewUserStore().FindByIdAsync(fault.CustomerId);
                var allTechnicians = NewRoleManager().FindByName(IdentityRoles.Technician.ToString()).Users.ToList();
                var user = await NewUserStore().FindByIdAsync(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId());

                return Json(new ResponseData()
                {
                    data = new { fault, user, customer },
                    success = true,
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new ResponseData()
                {
                    success = false,
                    message = $"Bir hata olustu {ex.Message}"
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> FaultFinish(FaultFinishViewModel model)
        {
            try
            {
                var teknisyen = await NewUserStore()
                    .FindByIdAsync(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId());
                var fault = new FaultRepo().GetById(model.FaultID);
                var operatorr = await NewUserStore().FindByIdAsync(fault.OperatorId);
                var customer = await NewUserStore().FindByIdAsync(fault.CustomerId);

                if (teknisyen != null && model.faultState == FaultState.Completed)
                {
                    fault.FaultState = model.faultState;
                    fault.FaultResultDate=DateTime.Now;;
                    fault.TechnicianDescription = model.TechnicianDescription;
                    fault.haveJob = false;
                    fault.TechnicianState = TechnicianState.Bosta;
                    new FaultRepo().Update(fault);
                    var Log = new FaultLog
                    {
                        TechnicianId = fault.TechnicianId,
                        CustomerId = fault.CustomerId,
                        Operation = fault.FaultState.ToString(),
                        FaultId = fault.FaultID,
                        OperationDescription = fault.FaultDescription


                    };
                    new FaultLogRepo().Insert(Log);
                    TempData["message"] =
                        $"{fault.FaultID} no'lu arıza {teknisyen.Name + " " + teknisyen.Surname} isimli teknisyen tarafından giderilmiştir.";
                    string SiteUrl = Request.Url.Scheme + System.Uri.SchemeDelimiter + Request.Url.Host +
                                     (Request.Url.IsDefaultPort ? "" : ":" + Request.Url.Port);

                    var emailService = new EmailService();
                    var body =
                        $"{fault.FaultID} no'lu arıza {teknisyen.Name + " " + teknisyen.Surname} isimli teknisyen tarafından giderilmiştir.";
                    await emailService.SendAsync(new IdentityMessage() {Body = body, Subject = "İş Durumu"},
                        operatorr.Email);
                    var bodyy =
                        $"{fault.FaultID} no'lu arızanız {teknisyen.Name + " " + teknisyen.Surname} isimli teknisyen tarafından giderilmiştir.<br>Bizi tercih ettiğiniz için teşekkür ederiz";
                    await emailService.SendAsync(new IdentityMessage() {Body = bodyy, Subject = "İş Durumu"},
                        customer.Email);


                }
                else if (teknisyen != null && model.faultState == FaultState.Pending)
                {
                    fault.FaultState = model.faultState;
                    new FaultRepo().Update(fault);
                    TempData["message"] =
                        $"{fault.FaultID} no'lu arıza {teknisyen.Name + " " + teknisyen.Surname} isimli teknisyen tarafından giderilememiştir.";
                    string SiteUrl = Request.Url.Scheme + System.Uri.SchemeDelimiter + Request.Url.Host +
                                     (Request.Url.IsDefaultPort ? "" : ":" + Request.Url.Port);
                    var emailService = new EmailService();
                    var body =
                        $"{fault.FaultID} no'lu arıza {teknisyen.Name + " " + teknisyen.Surname} isimli teknisyen tarafından giderilememiştir.<br>Açıklama:{model.TechnicianDescription} ";
                    await emailService.SendAsync(new IdentityMessage() {Body = body, Subject = "İş Durumu"},
                        operatorr.Email);
                }
                else
                    throw new Exception("İşlemi bitirirken hata oluştu");


            }
            catch (Exception ex)
            {
                TempData["message"] = ex.Message;
            }

            return RedirectToAction("Index", "Technician", model);
        }
    }
}