public CarListExResponse CarListEx(CarListExRequest carListExRequest)
        {
            if (carListExRequest.FromStation == carListExRequest.ToStation)
            {
                throw new ZException(new ZErrorRailway(Ufs.Framework.Fundamental.LibException.Railway.RZHDMsgCodes._Bad_Request));
            }
            RecursiveObjectValidator.Validate(carListExRequest);

            UpdateRequest(carListExRequest);

            if (IsStationForbidden(carListExRequest.FromCode) || IsStationForbidden(carListExRequest.ToCode))
            {
                throw new ZException(new ZErrorRailway(Ufs.Framework.Fundamental.LibException.Railway.RZHDMsgCodes.ElectronicTicketingInThisDirectionIsNotAvailable));
            }

            var providerDefinition = DetectorBuilder.Build(new ProviderProperty(carListExRequest), dataLayer, GetProviderSettingForSearch(carListExRequest.InterfaceType, carListExRequest.TrainNumber));
            if (providerDefinition.IsComplete)
            {
                var provider = _ProviderFactory.CreatePovider(providerDefinition.Provider, carListExRequest.PosMode, carListExRequest);

                //Валидация
                provider.Validation(carListExRequest);

                var from = carListExRequest.FromStationInfo;
                var to = carListExRequest.ToStationInfo;

                if (TransitRool.IsKaliningradAndRussianFederation(Tuple.Create(from.Code, from.Name), Tuple.Create(to.Code, to.Name)))
                {
                    throw new ZException(new ZErrorRailway(Ufs.Framework.Fundamental.LibException.Railway.RZHDMsgCodes._Kaliningrad_via_Litva_Unavailable));
                }

                try
                {
                    ILoyaltyService loyaltyService = null;

                    bool isBonusTicketsSearch = false;

                    if (!string.IsNullOrEmpty(carListExRequest.lpToken))
                    {
                        // проверяем авторизацию пользователя в АСУМД
                        loyaltyService = new LoyaltyService(UFSEnviroment.Test);

                        if (!loyaltyService.IsAuthorized(carListExRequest.lpToken))
                        {
                            throw new ZException(new ZErrorRailway(Ufs.Framework.Fundamental.LibException.Railway.RZHDMsgCodes._Not_Authorized_In_Loyalty_Program));
                        }

                        isBonusTicketsSearch = true;
                    }

                    if (provider is ExpressFinlandProvider && carListExRequest.PosMode == (byte) ProviderMode.Test)
                    {
                        carListExRequest.DepartureTime = null;
                    }

                    var result = provider.CarListEx(carListExRequest);
                    result.DirectionGroup = (DirectionGroup)Math.Max(from.DirectionGroup, to.DirectionGroup);

                    if (provider is InnovativeMobilityProvider)
                    {
                        var stations = dataLayer.GetStations(new[]
                        {
                            carListExRequest.FromCode ?? 0,
                            carListExRequest.ToCode ?? 0
                        }.ToList());

                        var isFromExists = stations.ContainsKey(carListExRequest.FromCode.Value);
                        var isToExists = stations.ContainsKey(carListExRequest.ToCode.Value);

                        result.FromStationCode = carListExRequest.FromCode.Value;
                        result.ToStationCode = carListExRequest.ToCode.Value;

                        if (isFromExists)
                        {
                            result.FromStation = stations[carListExRequest.FromCode.Value].Name;
                        }
                        if (isToExists)
                        {
                            result.ToStation = stations[carListExRequest.ToCode.Value].Name;
                        }
                    }

                    switch (carListExRequest.Language)
                    {
                        case Language.EN:
                            result.ToStation = to.NameEn;
                            result.FromStation = from.NameEn;
                            break;
                        case Language.DE:
                            result.ToStation = to.NameDe;
                            result.FromStation = from.NameDe;
                            break;
                    }

                    var serviceClasses = ServiceClassHelper.GetServiceClassesDb(db);
                    var servicesMap = CatalogHelper.GetCatalogServices();
                    var member = dataLayer.GetMember();

                    var clientFeeCalculation = CreateClientFeeCalculation(carListExRequest);
                    var pos = dataLayer.GetPos(carListExRequest.Pos);
                    foreach (var train in result.TrainInfo)
                    {
                        if (train.PassengerArrivalStation != null && train.PassengerArrivalStation.Code.HasValue)
                        {
                            train.PassengerArrivalStation = dataLayer.GetStationDescription(train.PassengerArrivalStation.Code.Value, carListExRequest.Language);
                        }
                        if (train.PassengerDepartureStation != null && train.PassengerDepartureStation.Code.HasValue)
                        {
                            train.PassengerDepartureStation = dataLayer.GetStationDescription(train.PassengerDepartureStation.Code.Value, carListExRequest.Language);
                        }

                        if (train.TrainArrivalStation.IsNotNullOrEmptyName())
                        {
                            train.TrainArrivalStation = dataLayer.GetStationDescription(train.TrainArrivalStation.Name, carListExRequest.Language, null, train.PassengerArrivalStation);
                        }

                        if (train.TrainDepartureStation.IsNotNullOrEmptyName())
                        {
                            train.TrainDepartureStation = dataLayer.GetStationDescription(train.TrainDepartureStation.Name, carListExRequest.Language, null, train.PassengerDepartureStation);
                        }

                        train.TrainName = train.GetTrainName(dataLayer, carListExRequest.DepartureDate, Language.RU);
                        
                        // каждому вагону указываем какому поезду он принадлежит
                        train.Cars.Where(c => c.Train == null).ForEach(a => { a.Train = train; });
                        train.RemoveForbiddenPlaces();
                        train.RemoveForbiddenCar(carListExRequest, result); //TODO избавится от двойного прохода по списку

						/*
						 * Для обратного выезда из Финляндии в ответах на справке не приходит перевозчик. 
						 * Необходимо подставлять самим ориентируясь на бренд поезда: Если Лев Толстой, то перевозчик ФПК
						 */
						if (provider is ExpressFinlandProvider && train.IsLevTolstoy() && train.Cars != null)
						{
							train.Cars.ForEach(c => c.CarrierName = Consts.Fpk);
						}

                        if (train.Cars != null)
                        {
                            //var isAllowedDomainForDelayedPayment = !(provider is InnovativeMobilityProvider) && IsAllowedDomainForDelayedPayment(carListExRequest);
                            var isAllowedDomainForDelayedPayment = false;
                            var isAllowedReservationForDeptDate = Helper.Helper.IsAllowedReservationForDeptDate(carListExRequest.DepartureDate, carListExRequest.Pos);

                            log.Info(string.Format("isAllowedReservationForDeptDate: {0}, DirectionGroup: {1}", isAllowedReservationForDeptDate, result.DirectionGroup));

                            foreach (var car in train.Cars)
                            {
                                if (isAllowedDomainForDelayedPayment && car.CarrierName != null)
                                {
                                    var isInternationalRoute = result.DirectionGroup != DirectionGroup.Local;
                                    car.DelayedPayment = IsAllowedDelayedPayment(car.CarrierName, carListExRequest.DepartureDate, isInternationalRoute);
                                }
                                car.CarTypeName = CarTypeHelper.GetCarTypeName(car.CarTypeName);

                                //TODO добавим классы обслуживания
                                if (!IsSwift(car.CarrierName, train.Brand, car.ServiceClass))//RZHD-3445
                                {
                                    var carTypeName = car.CarTypeName.ToUpper().Substring(0, 1);
                                    var access = new Access
                                    {
                                        TypeTrain = train.IsFirm ? "Firm" : "NoFirm",
                                        Carrier = car.CarrierName,
                                        ServiceClass = car.ServiceClass,
                                        CarType = carTypeName,
                                        Language = carListExRequest.Language.ToString(),
                                        NumberTrain = train.TrainNumber,
                                        CarDescription = car.AddSigns ?? string.Empty,
                                        DirectionGroup = result.DirectionGroup.ToString(),
                                        TrainBrand = train.Brand,
                                        TwoStorey = car.IsTwoStorey.ToString(),
                                        InternationalServiceClass = car.InternalServiceClass
                                    };

                                    car.CarServices = servicesMap.Items
                                        .Where(s => s.RuleCollection.CheckAllow(access))
                                        .Select(s => (CarService) Enum.Parse(typeof (CarService), s.Code))
                                        .ToList();

                                    car.ServiceClassDescription = serviceClasses.MayBeNull(
                                        s => s.ContainsKey(car.CarrierName)
                                            ? serviceClasses[car.CarrierName]
                                            : null).MayBeNull(c => c.ContainsKey(carTypeName)
                                                ? c[carTypeName]
                                                : null).MayBeNull(d => d.ContainsKey(car.ServiceClass)
                                                    ? d[car.ServiceClass]
                                                    : null).MayBeNull(sc => sc.Descriptions
                                                        .Where(p => p.RuleCollection.CheckAllow(access))
                                                        .Select(d => d.Value)
                                                        .FirstOrDefault());
                                }
                                if (RzhdSettings.WebConfig.DateKupekDiscountStart <= carListExRequest.DepartureDate &&
                                    RzhdSettings.WebConfig.DateKupekDiscountEnd >= carListExRequest.DepartureDate)
                                {
                                    if (RzhdSettings.WebConfig.TrainNumberKupekDiscount.Any())
                                    {
                                        car.IsAllowedBuyFullKupe = (!car.IsDm && !car.IsQm)
																   && (car.CarrierName.Equals(Consts.Fpk, StringComparison.InvariantCultureIgnoreCase) || car.CarrierName.Equals(Consts.Ldz, StringComparison.InvariantCultureIgnoreCase))
                                                                   && car.CarType == CarType.Coupe
                                                                   && RzhdSettings.WebConfig.TrainNumberKupekDiscount.Contains(train.TrainNumber);
                                    }
                                    else
                                    {
                                        car.IsAllowedBuyFullKupe = (!car.IsDm && !car.IsQm && !car.IsDynamicPricing)
																   && car.CarrierName.Equals(Consts.Fpk, StringComparison.InvariantCultureIgnoreCase)
                                                                   && car.CarType == CarType.Coupe;
                                    }
                                }

								if (!car.IsAllowedBuyFullKupe)
								{
									car.IsAllowedBuyFullKupe = RzhdSettings.WebConfig.SingleDiscounts.IsSatisfy(carListExRequest.DepartureDate, train.TrainNumber, car);
								}

                                car.Train.CarListExResponse = result;
                                if (!(RzhdSettings.WebConfig.SchemaNotUsedTrains.Contains(car.Train.TrainNumber) && car.CarrierName == "ДОСС"))
                                {
                                    if (!car.IsCoupeBuffet || car.CarrierName == "ДОСС")
                                    {
                                        foreach (var scheme in CarsSchemeHelper.ListCarsScheme)
                                        {
                                            var schemeName = scheme.Apply(car);
                                            if (!String.IsNullOrEmpty(schemeName))
                                            {
                                                car.Schema = schemeName;
                                                break;
                                            }
                                        }
                                    }
                                }
                                ServiceClassHelper.UpdateServiceClassDescription(car, train, carListExRequest.Language);

                                if (carListExRequest.TariffType != 0)
                                {
                                    car.AvailableTariffs = new List<int>
                                    {
                                        carListExRequest.TariffType
                                    };
                                }
                                else
                                {
                                    var tariffs = PassengerTariffHelper.GetAvailableTariff(car, result.DirectionGroup, train, result.FromStationCode, result.ToStationCode);
                                    if (tariffs.Any())
                                    {
                                        car.AvailableTariffs = tariffs.Select(x => (int)x).ToList();
                                    }
                                }

                                var loyaltyMap = CatalogHelper.GetCatalogLoyaltyCards();
                                if (String.IsNullOrEmpty(car.LoyaltyCards))
                                {
                                    if (IsAllegroLoyalty(train.Brand, train.TrainName))
                                    {
                                        train.Brand = Consts.Allegro;
                                        car.CarrierName = Consts.Doss;
                                    }

                                    var carrierExist = !String.IsNullOrEmpty(car.CarrierName);
									var carrier = carrierExist ? car.CarrierName : Consts.Fpk;

                                    if (carrierExist)
                                    {
                                        var accessForLoyaltyCards = new Access
                                        {
                                            Carrier = carrier,
                                            TrainBrand = train.Brand,
                                            ServiceClass = car.ServiceClass
                                        };

                                        var isAllowedMember = RzhdSettings.WebConfig.RzhdUniversalCardSetting.IsAllowedMember(member.member);
                                        var loyaltyCards = string.Join(",", loyaltyMap.Items
                                            .Where(s => s.RuleCollection.CheckAllow(accessForLoyaltyCards))
                                            .Where(s => s.Code != "RzhdU" || isAllowedMember)
                                            .Select(s => s.Code));

                                        if (!string.IsNullOrEmpty(loyaltyCards))
                                        {
                                            car.LoyaltyCards = loyaltyCards;
                                        }
                                    }
                                }
                                if (isAllowedReservationForDeptDate)
                                {
                                    AddReservationElementByTypeCar(train, car, member.member);
                                }
                            }
                        }

                        train.TrainName = train.GetTrainName(dataLayer, carListExRequest.DepartureDate, carListExRequest.Language);

                        if (GroupingTypesForJoinCars.Contains(carListExRequest.GroupingType) || provider is ExpressFinlandProvider)
                        {
                            JoinCars(train, true);
                        }

                        foreach (var car in train.Cars)
                        {
                            car.ClientCommission = GetClientComission(carListExRequest, car, clientFeeCalculation, isBonusTicketsSearch, carListExRequest.FromStationInfo, carListExRequest.ToStationInfo, pos, carListExRequest.DepartureDate, carListExRequest.GroupingType == GroupingTypeCarListEx.ByTypePlace);
                        }
                    }

                    result.TrainInfo = result.TrainInfo.Where(t => t.Cars.Any()).ToList();
                    if (!result.TrainInfo.Any())
                    {
                        throw new ZException(new ZErrorRailway(Ufs.Framework.Fundamental.LibException.Railway.RZHDMsgCodes._No_Places_At_This_Train));
                    }
                    var balance = dataLayer.CheckBalance(pos.test, Service.RzhdTickets);
                    result.Balance = balance.Sumbalance;
                    result.BalanceLimit = balance.Overdraft;
                    result.Language = carListExRequest.Language;
                    result.TimeZones = dataLayer.GetTimeZoneByStationCodes(new[] { result.FromStationCode.Value, result.ToStationCode.Value });


                    var fromStation = dataLayer.GetStationInfo(result.FromStationCode.ToString(), Language.RU, true);
                    var toStation = dataLayer.GetStationInfo(result.ToStationCode.ToString(), Language.RU, true);

                    var directionHelper = new DocumentTypeForDirectionHelper(fromStation.CountryCode, toStation.CountryCode);
                    result.AllowedDocTypes = directionHelper.GetAllowedDocumentTypeWithExtention(carListExRequest.InterfaceType, dataLayer.MemberByAuth.idgroup);

                    if (isBonusTicketsSearch)
                    {
                        result.TrainInfo.ForEach(t => t.Uid = Guid.NewGuid().ToString());
                        var trainFilteringRequest = result.ToTrainFilteringRequest();
                        var filteredResponse = loyaltyService.Filter(carListExRequest.lpToken, trainFilteringRequest);
                        result = LoyaltyServiceHelper.Merge(filteredResponse, result);
                    }

                    if ((pos.testType == 0 || pos.testType == 1) && provider.Provider != Provider.InnovativeMobility)
                    {
                        _rabbitMqPublisherConfig.PublishTrainsPrice(log, result);
                    }

                    return result;
                }
                catch (LookupStationException e)
                {
                    e.Mode = GetLoockupStationMode(from, to, e.LookupStations);
                    foreach (var station in e.LookupStations)
                    {
                        station.Name = dataLayer.GetStationName(station.Name, carListExRequest.Language);
                    }
                    throw;
                }
            }
            throw new NotImplementedException();
        }