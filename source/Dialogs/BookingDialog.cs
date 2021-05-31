using AdaptiveCards;
using Karamem0.BookingServiceBot.Models;
using Karamem0.BookingServiceBot.Resources;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Graph = Microsoft.Graph;

namespace Karamem0.BookingServiceBot.Dialogs
{

    public class BookingDialog : ComponentDialog
    {

        private readonly IStatePropertyAccessor<BookingProfile> bookingProfileAccessor;

        private readonly Graph.GraphServiceClient graphServiceClient;

        public BookingDialog(UserState userState, GraphServiceClientProvider graphServiceClientProvider) : base(nameof(BookingDialog))
        {
            this.bookingProfileAccessor = userState.CreateProperty<BookingProfile>(nameof(BookingProfile));
            this.graphServiceClient = graphServiceClientProvider.Value;
            this.AddDialog(
                new WaterfallDialog(
                    nameof(WaterfallDialog),
                    new WaterfallStep[]
                    {
                        this.ChoiceBookingBusinessAsync,
                        this.ChoiceBookingServiceAsync,
                        this.ChoiceBookingDateAsync,
                        this.ChoiceBookingTimeAsync,
                        this.EnterBookingCustomerNameAsync,
                        this.EnterBookingCustomerEmailAsync,
                        this.ConfirmBookingAsync,
                        this.SubmitBookingAsync,
                    }));
            this.AddDialog(new ChoicePrompt("BookingBusinessChoicePrompt"));
            this.AddDialog(new ChoicePrompt("BookingServiceChoicePrompt"));
            this.AddDialog(new ChoicePrompt("BookingDateChoicePrompt", this.ValidateBookingDateAsync));
            this.AddDialog(new ChoicePrompt("BookingTimeChoicePrompt"));
            this.AddDialog(new TextPrompt("BookingCustomerNameTextPrompt"));
            this.AddDialog(new TextPrompt("BookingCustomerEmailTextPrompt"));
            this.AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
        }

        private async Task<DialogTurnResult> ChoiceBookingBusinessAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // ビジネスの一覧を取得する
            var bookingBusinesses = (
                await this.graphServiceClient
                    .BookingBusinesses
                    .Request().GetAsync()
                )
                .Select(item => new KeyValuePair<string, string>(item.Id, item.DisplayName))
                .ToList();
            // 値を一時的なプロパティに格納する
            stepContext.Values["BookingBusinesses"] = bookingBusinesses;
            // ダイアログを作成する
            return await stepContext.PromptAsync(
                "BookingBusinessChoicePrompt",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(StringResources.ChooseBookingBusinessMessage),
                    Choices = ChoiceFactory.ToChoices(bookingBusinesses.Select(item => item.Value).ToList()),
                },
                cancellationToken);
        }

        private async Task<DialogTurnResult> ChoiceBookingServiceAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // プロファイルを取得する
            var bookingProfile = await this.bookingProfileAccessor.GetAsync(stepContext.Context, () => new BookingProfile(), cancellationToken);
            // 前のダイアログで選択されたビジネスを取得する
            var bookingBusinesses = (IList<KeyValuePair<string, string>>)stepContext.Values["BookingBusinesses"];
            var bookingBusinessId = bookingBusinesses[((FoundChoice)stepContext.Result).Index].Key;
            var bookingBusiness = await this.graphServiceClient
                .BookingBusinesses[bookingBusinessId]
                .Request().GetAsync();
            // ビジネスの情報をプロファイルに格納する
            bookingProfile.BookingBusinessId = bookingBusiness.Id;
            bookingProfile.BookingBusinessName = bookingBusiness.DisplayName;
            // スタッフを取得する
            var currentUser = await this.graphServiceClient.Me.Request().GetAsync();
            var bookingStaffMembers = await this.graphServiceClient
                .BookingBusinesses[bookingBusinessId]
                .StaffMembers
                .Request()
                .Filter($"emailAddress eq '{currentUser.Mail}'")
                .GetAsync();
            var bookingStaffMember = bookingStaffMembers.FirstOrDefault();
            // スタッフの情報をプロファイルに格納する
            bookingProfile.BookingBusinessId = bookingBusiness.Id;
            bookingProfile.BookingStaffMemberId = bookingStaffMember.Id;
            // 値を一時的なプロパティに格納する
            stepContext.Values["BookingAvailableTime"] = DateTime.Now.AddTicks(
                XmlConvert.ToTimeSpan(bookingBusiness.SchedulingPolicy.MinimumLeadTime.ToString()).Ticks);
            stepContext.Values["BookingBusinessHours"] = bookingBusiness.BusinessHours
                .Select(item =>
                {
                    if (item.TimeSlots.Any())
                    {
                        var start = item.TimeSlots.Select(time => new TimeSpan(time.Start.Hour, time.Start.Minute, time.Start.Second)).Min();
                        var end = item.TimeSlots.Select(time => new TimeSpan(time.End.Hour, time.End.Minute, time.End.Second)).Min();
                        var interval = XmlConvert.ToTimeSpan(bookingBusiness.SchedulingPolicy.TimeSlotInterval.ToString());
                        var list = new List<TimeSpan>();
                        for (var current = start; current < end; current = current.Add(interval))
                        {
                            if (item.TimeSlots.Any(time =>
                                current >= new TimeSpan(time.Start.Hour, time.Start.Minute, time.Start.Second) ||
                                current < new TimeSpan(time.End.Hour, time.End.Minute, time.End.Second)))
                            {
                                list.Add(current);
                            }
                        }
                        return new KeyValuePair<DayOfWeek, IList<TimeSpan>>((DayOfWeek)item.Day, list);
                    }
                    else
                    {
                        return new KeyValuePair<DayOfWeek, IList<TimeSpan>>((DayOfWeek)item.Day, Enumerable.Empty<TimeSpan>().ToList());
                    }
                })
                .ToDictionary(item => item.Key, item => item.Value);
            // サービスの一覧を取得する
            var bookingServices = (
                await this.graphServiceClient
                    .BookingBusinesses[bookingBusinessId]
                    .Services
                    .Request().GetAsync()
                )
                .Select(item => new KeyValuePair<string, string>(item.Id, item.DisplayName))
                .ToList();
            // 値を一時的なプロパティに格納する
            stepContext.Values["BookingServices"] = bookingServices;
            // ダイアログを作成する
            return await stepContext.PromptAsync(
                "BookingServiceChoicePrompt",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(StringResources.ChooseBookingServiceMessage),
                    Choices = ChoiceFactory.ToChoices(bookingServices.Select(item => item.Value).ToList()),
                },
                cancellationToken);
        }

        private async Task<DialogTurnResult> ChoiceBookingDateAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // プロファイルを取得する
            var bookingProfile = await this.bookingProfileAccessor.GetAsync(stepContext.Context, () => new BookingProfile(), cancellationToken);
            // 前のダイアログで選択されたサービスを取得する
            var bookingServices = (IList<KeyValuePair<string, string>>)stepContext.Values["BookingServices"];
            var bookingServiceId = bookingServices[((FoundChoice)stepContext.Result).Index].Key;
            var bookingService = await this.graphServiceClient
                .BookingBusinesses[bookingProfile.BookingBusinessId]
                .Services[bookingServiceId]
                .Request().GetAsync();
            // サービスの情報をプロファイルに格納する
            bookingProfile.BookingServiceId = bookingService.Id;
            bookingProfile.BookingServiceName = bookingService.DisplayName;
            // 値を一時的なプロパティに格納する
            stepContext.Values["BookingDuration"] = XmlConvert.ToTimeSpan(bookingService.DefaultDuration.ToString());
            // 日付の一覧を取得する
            var bookingAvailableTime = (DateTime)stepContext.Values["BookingAvailableTime"];
            var bookingDates = Enumerable.Range(0, 6)
                .Select(item => bookingAvailableTime.AddDays(item).Date)
                .Select(item => new KeyValuePair<DateTime, string>(item, item.ToString("m")))
                .ToList();
            // 値を一時的なプロパティに格納する
            stepContext.Values["BookingDates"] = bookingDates;
            // ダイアログを作成する
            return await stepContext.PromptAsync(
                "BookingDateChoicePrompt",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(StringResources.ChooseBookingDateMessage),
                    RetryPrompt = MessageFactory.Text(StringResources.RetryBookingDateMessage),
                    Choices = ChoiceFactory.ToChoices(bookingDates.Select(item => item.Value).ToList()),
                    Validations = stepContext.Values,
                },
                cancellationToken);
        }

        private async Task<bool> ValidateBookingDateAsync(PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken)
        {
            var values = (IDictionary<string, object>)promptContext.Options.Validations;
            // 前のダイアログで選択された日付を取得する
            var bookingDates = (IList<KeyValuePair<DateTime, string>>)values["BookingDates"];
            var bookingDateId = bookingDates[promptContext.Recognized.Value.Index].Key;
            // 時刻の一覧を取得する
            var bookingAvailableTime = (DateTime)values["BookingAvailableTime"];
            var bookingBusinessHours = (IDictionary<DayOfWeek, IList<TimeSpan>>)values["BookingBusinessHours"];
            var bookingTimes = bookingBusinessHours[bookingDateId.DayOfWeek]
                .Select(item => bookingDateId.Date.AddTicks(item.Ticks))
                .Where(item => item > bookingAvailableTime)
                .ToList();
            return await Task.FromResult(bookingTimes.Any());
        }

        private async Task<DialogTurnResult> ChoiceBookingTimeAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // プロファイルを取得する
            var bookingProfile = await this.bookingProfileAccessor.GetAsync(stepContext.Context, () => new BookingProfile(), cancellationToken);
            // 前のダイアログで選択された日付を取得する
            var bookingDates = (IList<KeyValuePair<DateTime, string>>)stepContext.Values["BookingDates"];
            var bookingDateId = bookingDates[((FoundChoice)stepContext.Result).Index].Key;
            // 時刻の一覧を取得する
            var bookingAvailableTime = (DateTime)stepContext.Values["BookingAvailableTime"];
            var bookingBusinessHours = (IDictionary<DayOfWeek, IList<TimeSpan>>)stepContext.Values["BookingBusinessHours"];
            var bookingTimes = bookingBusinessHours[bookingDateId.DayOfWeek]
                .Select(item => bookingDateId.Date.AddTicks(item.Ticks))
                .Where(item => item > bookingAvailableTime)
                .Select(item => new KeyValuePair<DateTime, string>(item, item.ToString("t")))
                .ToList();
            // 値を一時的なプロパティに格納する
            stepContext.Values["BookingTimes"] = bookingTimes;
            // ダイアログを作成する
            return await stepContext.PromptAsync(
                "BookingTimeChoicePrompt",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(StringResources.ChooseBookingTimeMessage),
                    Choices = ChoiceFactory.ToChoices(bookingTimes.Select(item => item.Value).ToList()),
                },
                cancellationToken);
        }

        private async Task<DialogTurnResult> EnterBookingCustomerNameAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // プロファイルを取得する
            var bookingProfile = await this.bookingProfileAccessor.GetAsync(stepContext.Context, () => new BookingProfile(), cancellationToken);
            // 前のダイアログで選択された日付を取得する
            var bookingTimes = (IList<KeyValuePair<DateTime, string>>)stepContext.Values["BookingTimes"];
            var bookingTimeId = bookingTimes[((FoundChoice)stepContext.Result).Index].Key;
            var bookingDuration = TimeSpan.Parse(stepContext.Values["BookingDuration"].ToString());
            // 日時の情報をプロファイルに格納する
            bookingProfile.BookingStartTime = bookingTimeId;
            bookingProfile.BookingEndTime = bookingTimeId.AddTicks(bookingDuration.Ticks);
            // ダイアログを作成する
            return await stepContext.PromptAsync(
                "BookingCustomerNameTextPrompt",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(StringResources.EnterBookingCustomerNameMessage)
                },
                cancellationToken);
        }

        private async Task<DialogTurnResult> EnterBookingCustomerEmailAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // プロファイルを取得する
            var bookingProfile = await this.bookingProfileAccessor.GetAsync(stepContext.Context, () => new BookingProfile(), cancellationToken);
            // 前のダイアログで入力された名前を取得する
            var bookingCustomerName = (string)stepContext.Result;
            // 名前の情報をプロファイルに格納する
            bookingProfile.BookingCustomerName = bookingCustomerName;
            // ダイアログを作成する
            return await stepContext.PromptAsync(
                "BookingCustomerEmailTextPrompt",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(StringResources.EnterBookingCustomerEmailMessage)
                },
                cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmBookingAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // プロファイルを取得する
            var bookingProfile = await this.bookingProfileAccessor.GetAsync(stepContext.Context, () => new BookingProfile(), cancellationToken);
            // 前のダイアログで入力された電子メール アドレスを取得する
            var bookingCustomerEmail = (string)stepContext.Result;
            // 電子メール アドレスに一致する顧客を取得する
            var bookingCustomer = default(Graph.BookingCustomer);
            var bookingCustomerIterator = Graph.PageIterator<Graph.BookingCustomer>.CreatePageIterator(
                this.graphServiceClient,
                await this.graphServiceClient
                    .BookingBusinesses[bookingProfile.BookingBusinessId]
                    .Customers
                    .Request()
                    .GetAsync(),
                value =>
                {
                    if (value.EmailAddress == bookingCustomerEmail)
                    {
                        bookingCustomer = value;
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                });
            await bookingCustomerIterator.IterateAsync();
            // 電子メール アドレスの情報をプロファイルに格納する
            bookingProfile.BookingCustomerId = bookingCustomer?.Id;
            bookingProfile.BookingCustomerEmail = bookingCustomerEmail;
            // アダプティブ カードを作成する
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 3))
            {
                Body = new List<AdaptiveElement>()
                {
                    new AdaptiveFactSet()
                    {
                        Facts = new List<AdaptiveFact>()
                        {
                            new AdaptiveFact(StringResources.BookingBusinessLabel, bookingProfile.BookingBusinessName),
                            new AdaptiveFact(StringResources.BookingServiceLabel, bookingProfile.BookingServiceName),
                            new AdaptiveFact(StringResources.BookingStartTimeLabel, bookingProfile.BookingStartTime.ToString("g")),
                            new AdaptiveFact(StringResources.BookingEndTimeLabel, bookingProfile.BookingEndTime.ToString("g")),
                            new AdaptiveFact(StringResources.BookingCustomerNameLabel, bookingProfile.BookingCustomerName),
                            new AdaptiveFact(StringResources.BookingCustomerEmailLabel, bookingProfile.BookingCustomerEmail),
                        }
                    }
                }
            };
            // アダプティブ カードを送信する
            await stepContext.Context.SendActivityAsync(MessageFactory.Attachment(new Attachment()
            {
                ContentType = AdaptiveCard.ContentType,
                Content = JObject.FromObject(card),
            }));
            // ダイアログを作成する
            return await stepContext.PromptAsync(
                nameof(ConfirmPrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(StringResources.ConfirmBookingMessage),
                },
                cancellationToken);
        }

        private async Task<DialogTurnResult> SubmitBookingAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                // プロファイルを取得する
                var bookingProfile = await this.bookingProfileAccessor.GetAsync(stepContext.Context, () => new BookingProfile(), cancellationToken);
                try
                {
                    // 予約を作成する
                    await this.graphServiceClient
                        .BookingBusinesses[bookingProfile.BookingBusinessId]
                        .Appointments
                        .Request()
                        .AddAsync(new Graph.BookingAppointment()
                        {
                            CustomerId = bookingProfile.BookingCustomerId,
                            CustomerName = bookingProfile.BookingCustomerName,
                            CustomerEmailAddress = bookingProfile.BookingCustomerEmail,
                            End = new Graph.DateTimeTimeZone()
                            {
                                DateTime = bookingProfile.BookingEndTime.ToUniversalTime().ToString("s"),
                                TimeZone = "UTC",
                            },
                            ServiceId = bookingProfile.BookingServiceId,
                            ServiceName = bookingProfile.BookingServiceName,
                            Start = new Graph.DateTimeTimeZone()
                            {
                                DateTime = bookingProfile.BookingStartTime.ToUniversalTime().ToString("s"),
                                TimeZone = "UTC",
                            },
                            StaffMemberIds = new[] { bookingProfile.BookingStaffMemberId }
                        });
                    // メッセージを送信する
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(StringResources.CompleteBookingMessage));
                }
                catch (Exception ex)
                {
                    // メッセージを送信する
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(ex.Message));
                }
            }
            else
            {
                // メッセージを送信する
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(StringResources.CancelBookingMessage));
            }
            // ダイアログを終了する
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

    }

}
