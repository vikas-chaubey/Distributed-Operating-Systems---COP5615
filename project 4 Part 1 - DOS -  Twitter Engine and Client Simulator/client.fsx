// ActorSayHello.fsx
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"


open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open System.Collections.Generic
open System.Collections.Concurrent
open System.Diagnostics
open System.Threading

//Declared types for matching cases within actor
type RegisterUserOnTwitterApp = int   
type RegistrationStatus =  bool * string  // username passowrd

type Login = int * int 
type LoginStatus = bool * bool * string
type Logout = int * int * int 
type LogoutStatus = bool * bool * bool * string
type session = int * int * int * int 
type subscription = int * int * int * int * int
type subscriptionStatus = bool * bool * bool * bool * string
type Tweet = int * int * int * int * int * int
type TweetStatus = int * float * string
type SubscriptionFeed = int * int * int * int * int * int * string * string
type Retweet = float * string * string
type RetweetStatus = int * float * float * string
type HashTagQuery = float * string
type MentionsQuery =   float  * float * string
type GetUsersTweetQuery = float  * float * float * string
type HashTagQueryResponse = bool * string * string 
type MentionsQueryResponse = bool * bool * string * string 
type GetUsersTweetResponse = bool * bool * bool * string * string
type FollowAnotherUser = int * string * string   
type MessageFromServer = string



let mutable ExecutionShutDownFlag = false
let mutable query1Flag : bool = false
let mutable query2Flag : bool = false
let mutable query3Flag : bool = false
let mutable serverIdCounter : int = 0
let mutable TotalNumberofUsers : int = 0
let mutable TotalNumberOfTweets : int = 200
let mutable SessionTimeFOrEachUserInSeconds : int = 60
let TotalNumberOFServerInstances = 128

let args : string array = fsi.CommandLineArgs |>  Array.tail//
//fetch the first argument - total number of nodes in the system
TotalNumberofUsers <- args.[0] |> int

//These stopwatches are used to calculate the roundtrip time of different endpoints when simulation is running to evaluate system performance under heavy load.
let mutable registrationEndPointPerformanceTimer = new Stopwatch()
let mutable LoginEndPointPerformanceTimer = new Stopwatch()
let mutable SubscriptionEndPointPerformanceTimer = new Stopwatch()
let mutable TweetEndpointPointPerformanceTimer = new Stopwatch()
let mutable RetweetEndpointPointPerformanceTimer = new Stopwatch()
let mutable LogoutEndPointPerformanceTimer = new Stopwatch()
let mutable HashTagQueryEndpointPointPerformanceTimer = new Stopwatch()
let mutable MentionsQueryEndpointPointPerformanceTimer = new Stopwatch()
let mutable UserTweetsQueryEndpointPointPerformanceTimer= new Stopwatch()

//flags to turn the stopwatches on only once
let mutable registrationWatchFlag = 1
let mutable LoginWatchFlag = 1
let mutable SubscriptionWatchFlag = 1
let mutable TweetWatchFlag = 1
let mutable LogoutWatchFlag = 1
let mutable RetweetWatchFlag = 1
let mutable HashTagWatchFlag = 1
let mutable MentionsWatchFlag = 1
let mutable UsersTweetWatchFlag = 1


let mutable SuccessfullyRegisteredUsersCounter : int  = 0
let mutable SuccessfullyLoggedinUsersCounter : int  = 0
let mutable SuccessfullyLoggedOutUsersCounter : int  = 0
let mutable SuccessfullySubscriptionUsersCounter : int  = 0
let mutable SuccessfulTweets : int = 0

let hashTags = [| "#life"; "#politics"; "#art" ; "#electionNews" ; "#influencer" ; "#marketting";"#realestate" ; "#crypto" ; "#womenshistorymonth" ; "#pets"|]
let mentions : List<string> = new List<string>() 

let mutable usedHashTag : string = ""
let mutable usedMention : string = ""

let getServerID() : int =
    if (serverIdCounter >= TotalNumberOFServerInstances) then
        serverIdCounter <- 0
    serverIdCounter <- serverIdCounter + 1
    serverIdCounter


let shuffleArray (arrayArg : array<string>) = 
    for i in 0 .. (arrayArg.Length - 1 ) do
        let j = (System.Random()).Next(arrayArg.Length)
        let tempItem = arrayArg.[j]
        arrayArg.[j] <- arrayArg.[i]
        arrayArg.[i] <- tempItem

//contains userId as keys  and list of other users which that userId will follow on twitterengine (claculated with zipf distribution)
let mutable ZipFDistributedSubscriptionMap : Dictionary<string, List<string>> = new Dictionary<string, List<string>>() 

// contains userIds as key and number of tweets , this userId can make in a session , (claculated with zipf distribution)
let mutable ZipFDistributedTweetMap : Dictionary<string, int> = new Dictionary<string, int>()

let prepareZipFDistributionForTweetsAndSubscribers(totalNumberofUsers : int , totalNumberOfTweets : int) =   
    let mutable userIdArray : string array = Array.zeroCreate TotalNumberofUsers
    for counter in 1 .. TotalNumberofUsers do
        let mutable userId : string = "user" + string(counter)
        userIdArray.[counter - 1] <- userId 
        let mutable subscriptionList = new List<string>()
        ZipFDistributedSubscriptionMap.Add(userId,subscriptionList)

    for counter2 in 1 .. TotalNumberofUsers do
        //variable to hold the rank of userID in zipF distribution for deciding the frequency of its subscriptions and tweets , highest rank user will have most subscribers and will do most tweets.
        let mutable rankOfUser : int = counter2 
        // zipF constant
        let zipFConstant = 0.3 
        let mutable userId : string = "user" + string(counter2)

        //calculate number of user which will follow current user using zipF distribution formula 
        //ZipF Distribution Formula  :    (frequency of occurrence of specific element (freq) = ((zipf Constant) * (Total Elements in Distribution) / (Rank of specific element))
        let mutable numberOfSubscribersFollowingUser : int = Convert.ToInt32(Math.Floor( (zipFConstant * (float TotalNumberofUsers) ) / (float rankOfUser) ) )
        //calculate number of Tweets which will be made by user using zipF distribution formula 
        //ZipF Distribution Formula  :    (frequency of occurrence of specific element (freq) = ((zipf Constant) * (Total Elements in Distribution) / (Rank of specific element))
        let mutable numberOfTweetsByUser : int = Convert.ToInt32(Math.Ceiling( (zipFConstant * (float TotalNumberOfTweets) ) / (float rankOfUser) ) )
        //to randomly select certain number of users (numberOfSubscribersFollowingUser) which will follow current userId, shuffle this array which has all userIds 
        shuffleArray(userIdArray)
        ZipFDistributedTweetMap.Add(userId , numberOfTweetsByUser)
        //add current user to subscription List of all randomly selected other users (count will be equal to numberOfSubscribersFollowingUser), which will follow current user
        let mutable i : int = 1
        while i <= numberOfSubscribersFollowingUser do 
            if(userIdArray.[i-1] <> userId)  then
               let mutable subscriptionListOfUser : List<string> = ZipFDistributedSubscriptionMap.Item(userIdArray.[i-1])
               subscriptionListOfUser.Add(userId)
               i <- i + 1

//prepare zipf distribution for users followeres and tweets
prepareZipFDistributionForTweetsAndSubscribers(TotalNumberofUsers , TotalNumberOfTweets)


//prepare Remote configuration
let config =
    ConfigurationFactory.ParseString(
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = 0.0.0.0
                port = 8080
            }
        }")
let system = System.create "ClientActorSystem" config

type UserActor(usernameArg : string , passwordArg : string , sessionTimeArg : int , subscriptionCountArg : int , susbcriberListArg : List<string> , numberOfTweetsArg : int , tweetRateperSecondArg : int) =
  inherit Actor()
  let mutable username : string = usernameArg
  let mutable password : string = passwordArg
  let mutable sessionTime : int = sessionTimeArg
  let mutable subscriptionCount : int = subscriptionCountArg
  let mutable susbcritionList : List<string> = susbcriberListArg
  let mutable numberOfTweets : int = numberOfTweetsArg
  let mutable tweetRateperSecond : int = tweetRateperSecondArg
  let mutable successfulSubscriptionCounter : int = 0
  let mutable numberOfTweets : int = numberOfTweetsArg
  let mutable isRegistrationSuccessful : bool = false
  let mutable isLoginSuccessful : bool = false
  let mutable isLogoutSuccessful : bool = false
  let mutable isSubscriptionSuccessful : bool = false
  let mutable logOutFlag : bool = false

  override x.OnReceive message =
     match message with
     | :? RegisterUserOnTwitterApp as msg -> if (registrationWatchFlag = 1) then
                                                 registrationWatchFlag <- 0 
                                                 try
                                                    registrationEndPointPerformanceTimer.Start()
                                                 with 
                                                    | _ -> printf ""
                                             let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                                             let mutable serverRef = system.ActorSelection(serverPath)
                                             serverRef <! (username , password)

     | :? RegistrationStatus as msg ->  let mutable (userRegisterationStatus , serverMessage) = unbox<RegistrationStatus> msg    
                                        if (userRegisterationStatus) then
                                            isRegistrationSuccessful <- true 
                                            SuccessfullyRegisteredUsersCounter <- SuccessfullyRegisteredUsersCounter + 1
                                            if(SuccessfullyRegisteredUsersCounter = TotalNumberofUsers) then
                                                  try
                                                     registrationEndPointPerformanceTimer.Stop()
                                                  with 
                                                     | _ -> printf ""
                                            printfn "Server Response : %s " serverMessage
                                            x.Self <! (1,1)
                                        else      
                                            username <- username + "new"
                                            x.Self <! (username , password)
                                         
     | :? Login as msg -> if (isRegistrationSuccessful) then
                              if(LoginWatchFlag = 1) then
                                  LoginWatchFlag <- 0 
                                  try
                                     LoginEndPointPerformanceTimer.Start()
                                  with 
                                     | _ -> printf ""
                              let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                              let mutable serverRef = system.ActorSelection(serverPath)
                              serverRef <! (1, 1, 1 , username , password)

                          else
                              printfn "User with userId : %s can not login becuase user registration was unsuccessful" username
                   
     | :? LoginStatus as msg -> let mutable (userLoginStatus ,arg2, serverMessage) = unbox<LoginStatus> msg    
                                if (userLoginStatus) then
                                    isLoginSuccessful <- true 
                                    SuccessfullyLoggedinUsersCounter <- SuccessfullyLoggedinUsersCounter + 1
                                    if (SuccessfullyLoggedinUsersCounter = TotalNumberofUsers) then
                                          try
                                              LoginEndPointPerformanceTimer.Stop()
                                          with 
                                              | _ -> printf ""
                                    printfn "Server Response : %s " serverMessage
                                    // start subscription
                                    x.Self <! (1,1,1,1,1)
                                    // start session
                                    //x.Self <! (1,1,1,1)
                                else      
                                    printfn "Server Response : %s " serverMessage    


     | :? subscription as msg -> if (subscriptionCount = 0) then
                                   // start session
                                    x.Self <! (1,1,1,1)
                                 else
                                    if(SubscriptionWatchFlag = 1) then
                                        SubscriptionWatchFlag <- 0 
                                        try
                                            SubscriptionEndPointPerformanceTimer.Start()
                                        with 
                                            | _ -> printf ""
                                    for i in 0 .. (susbcritionList.Count - 1) do
                                        let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                                        let mutable serverRef = system.ActorSelection(serverPath)
                                        serverRef <! (1, username , susbcritionList.Item(i))                                
                          
     | :? session as msg -> let mutable sessionLogOutTime : DateTime = (DateTime.Now).AddSeconds(float sessionTime)
                            // start tweeting
                            x.Self <! (1,1,1,1,1,1)
                            //keep session alive for given session duration
                            while (DateTime.Now < sessionLogOutTime) do
                               printf ""
                            logOutFlag <- true
                            //session completed , logout
                            x.Self <! (1,1,1)


     | :? Tweet as msg -> let mutable sessionLogOutTime : DateTime = (DateTime.Now).AddSeconds(float sessionTime)  
                          let mmutable tweetTime : DateTime = (DateTime.Now).AddSeconds(float 1) 
                          let mutable tweetNumber : int = 0
                          let timer = new Timers.Timer(1000.0)
                          let event = Async.AwaitEvent (timer.Elapsed) |> Async.Ignore
                          //printfn "%A" DateTime.Now
                          timer.Start()
                          while (DateTime.Now < sessionLogOutTime) && (logOutFlag) && (tweetNumber < numberOfTweets ) do
                            Async.RunSynchronously event
                            let mutable tweetsCounter : int = 0
                            while (tweetsCounter <= tweetRateperSecond) do
                                 // SEND TWEET
                                 let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                                 let mutable serverRef = system.ActorSelection(serverPath)
                                 let hashtagRandomSelect = System.Random().Next(10)
                                 let mentionRandomSelect = System.Random().Next(mentions.Count)
                                 usedHashTag <- hashTags.[hashtagRandomSelect]
                                 usedMention <- mentions.Item(mentionRandomSelect)
                                 let mutable tweet : string = "I am Tweeting" + " " + mentions.Item(mentionRandomSelect) + " " + hashTags.[hashtagRandomSelect]
                                 try
                                       serverRef <! (tweetNumber, 1, username , tweet)  
                                 with
                                       | _ ->  serverRef <! (tweetNumber, 1, username , tweet) 
                                 tweetsCounter <- tweetsCounter + 1
                                 tweetNumber <- tweetNumber + 1                       

     
                                   
     | :? subscriptionStatus as msg ->  let mutable (userSubscriptionStatus , arg2, agr3, arg4, serverMessage) = unbox<subscriptionStatus> msg    
                                        if (userSubscriptionStatus) then
                                            isSubscriptionSuccessful <- true
                                            printfn "Server Response : %s " serverMessage
                                            successfulSubscriptionCounter <- successfulSubscriptionCounter + 1

                                            if (successfulSubscriptionCounter = subscriptionCount) then
                                                //start session
                                                x.Self <! (1,1,1,1)
                                                SuccessfullySubscriptionUsersCounter <- SuccessfullySubscriptionUsersCounter + 1
                                                if(SuccessfullySubscriptionUsersCounter = TotalNumberofUsers) then
                                                     try
                                                         SubscriptionEndPointPerformanceTimer.Stop()
                                                     with 
                                                         | _ -> printf ""
                                        else      
                                            printfn "Server Response : %s " serverMessage     


     | :? TweetStatus as msg -> let mutable (int1, float1 , serverResponse) = unbox<TweetStatus> msg                          
                                printfn "%s" serverResponse
                                SuccessfulTweets <- SuccessfulTweets + 1


     | :? SubscriptionFeed as msg -> let mutable (int1,int2,int3,int4,int5,int6, subscriptionUserName , tweet) = unbox<SubscriptionFeed> msg
                                     let randomDecisionForRetweet = System.Random().Next(1,20)                                   
                                     if(randomDecisionForRetweet = 2) then
                                        x.Self <! (1.5 , subscriptionUserName,tweet)
                                     else
                                        printf ""



     | :? Retweet as msg -> let mutable (float1,subscriptionUserName , tweet) = unbox<Retweet> msg
                            //printfn "%s Retweeted Tweet From : %s , Tweet : %s " username subscriptionUserName tweet
                            let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                            let mutable serverRef = system.ActorSelection(serverPath)
                            try
                                serverRef <! (1, 1, 1, 1, 1, subscriptionUserName, username ,tweet)  
                            with
                                | _ -> serverRef <! (1, 1, 1, 1, 1, subscriptionUserName, username ,tweet)  

     | :? RetweetStatus as msg -> let mutable (int1, float1 , float2, serverResponse) = unbox<RetweetStatus> msg                          
                                  printfn "%s" serverResponse

     | :? HashTagQuery as msg ->if(HashTagWatchFlag = 1) then
                                    HashTagWatchFlag <- 0
                                    try
                                        HashTagQueryEndpointPointPerformanceTimer.Start()
                                    with
                                        | _ -> printf ""
                                let mutable (float1 , hashtag) = unbox<HashTagQuery> msg
                                let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                                let mutable serverRef = system.ActorSelection(serverPath)
                                try
                                    serverRef <! (1, 1, 1, hashtag)  
                                with
                                    | _ -> serverRef <! (1, 1, 1, hashtag)   

     | :? MentionsQuery as msg -> if(MentionsWatchFlag = 1) then
                                    MentionsWatchFlag <- 0
                                    try
                                        MentionsQueryEndpointPointPerformanceTimer.Start()
                                    with
                                        | _ -> printf ""
                                  let mutable (float1 , float2, mention) = unbox<MentionsQuery> msg
                                  let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                                  let mutable serverRef = system.ActorSelection(serverPath)
                                  try
                                       serverRef <!  (1, 1, 1, 1, mention) 
                                  with
                                       | _ -> serverRef <! (1, 1, 1, 1, mention)     

     | :? GetUsersTweetQuery as msg ->  if(UsersTweetWatchFlag = 1) then
                                            UsersTweetWatchFlag <- 0
                                            try
                                                UserTweetsQueryEndpointPointPerformanceTimer.Start()
                                            with
                                                | _ -> printf ""
                                        let mutable (float1 , float2, float3, userId) = unbox<GetUsersTweetQuery> msg
                                        let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                                        let mutable serverRef = system.ActorSelection(serverPath)
                                        try
                                            serverRef <! (1, 1, 1, 1, 1 , userId)
                                        with
                                            | _ -> serverRef <! (1, 1, 1, 1, 1 , userId)                     

     | :? HashTagQueryResponse as msg -> try
                                             HashTagQueryEndpointPointPerformanceTimer.Stop()
                                         with
                                             | _ -> printf ""
                                         let mutable (queryBoolStatus , hashtag , responseString) = unbox<HashTagQueryResponse> msg 
                                         if queryBoolStatus then
                                             printfn "Server Response : All Tweets Containng Hashtag : %s are : %s" hashtag responseString
                                         else
                                            printfn "Server Response : No Tweet Found which Contains Hashtag : %s " hashtag 
                                         query1Flag <- true

     | :? MentionsQueryResponse as msg -> try
                                              MentionsQueryEndpointPointPerformanceTimer.Stop()
                                          with
                                              | _ -> printf ""
                                          let mutable (queryBoolStatus , bool2, mention , responseString) = unbox<MentionsQueryResponse> msg 
                                          if queryBoolStatus then
                                              printfn "Server Response : All Tweets Containng mention : %s are : %s" mention responseString
                                          else
                                              printfn "Server Response : No Tweet Found which Contains mention : %s " mention
                                          query2Flag <- true

     | :? GetUsersTweetResponse as msg -> try
                                              UserTweetsQueryEndpointPointPerformanceTimer.Stop()
                                          with
                                              | _ -> printf ""
                                          let mutable (queryBoolStatus ,bool2,bool3, username , responseString) = unbox<GetUsersTweetResponse> msg 
                                          if queryBoolStatus then
                                              printfn "Server Response : All Tweets from User with UserId: %s are  :  %s" username responseString
                                          else
                                              printfn "Server Response : No Tweet Found from User with UserId: %s" username    
                                          query3Flag <- true                                   

     | :? Logout as msg -> if(LogoutWatchFlag = 1) then
                                  LogoutWatchFlag <- 0 
                                  try
                                     LogoutEndPointPerformanceTimer.Start()
                                  with 
                                     | _ -> printf ""    
                                  x.Self <! (1.7 , usedHashTag)
                                  x.Self <! (1.7 ,1.4, usedMention)
                                  x.Self <! (1.7 ,1.5 ,2.3, "user6")                           
                           let mutable serverPath : string = "akka.tcp://ServerActorSystem@localhost:9000/user/server" + string(getServerID())
                           let mutable serverRef = system.ActorSelection(serverPath)
                           try
                                serverRef <! (1, 1, 1, 1, username , password)  
                           with
                                | _ -> serverRef <! (1, 1, 1, 1, username , password)  
     
     | :? LogoutStatus as msg -> let mutable (userLogoutStatus , arg2, arg3, serverMessage) = unbox<LogoutStatus> msg    
                                 if (userLogoutStatus) then
                                    isLogoutSuccessful <- true 
                                    printfn "Server Response : %s " serverMessage
                                    SuccessfullyLoggedOutUsersCounter <- SuccessfullyLoggedOutUsersCounter + 1
                                    if(SuccessfullyLoggedOutUsersCounter = SuccessfullyLoggedinUsersCounter ) then                                          
                                        try
                                            LogoutEndPointPerformanceTimer.Stop()
                                        with
                                            | _ -> printf ""
                                          
                                        ExecutionShutDownFlag <- true 
                                                                       
                                 else      
                                    printfn "Server Response : %s " serverMessage   
                                 

     | :? MessageFromServer as msg -> printfn "%s" msg

     | _ -> failwith "wrong message format for child node"





let userActorRefArray : IActorRef array = Array.zeroCreate TotalNumberofUsers
printfn "hello............."
//Create and simulate user and start their registration
for i in 1 .. TotalNumberofUsers do
    let mutable userId : string = "user" + string(i)
    mentions.Add("@" + userId)
    let mutable password = userId + string(123)
    let mutable sessionTimeInSeconds : int = SessionTimeFOrEachUserInSeconds  // time interval between user login and logout
    let mutable subscriptionList : List<string> = ZipFDistributedSubscriptionMap.Item(userId)
    let mutable numberOfTweetsForUser : int = ZipFDistributedTweetMap.Item(userId)
    let mutable subscriptionCount : int = subscriptionList.Count
    let mutable tweetRateperSecond : int = Convert.ToInt32(Math.Ceiling( (float numberOfTweetsForUser) / (float sessionTimeInSeconds) ) )
    printfn "userId : %s subscriptionCount : %i numberOfTweetsForUser : %i tweetRateperSecond : %i" userId subscriptionCount numberOfTweetsForUser tweetRateperSecond
    userActorRefArray.[i-1] <- system.ActorOf(Props(typedefof<UserActor>,[|box userId;  box password ; box sessionTimeInSeconds ; box subscriptionCount ; box subscriptionList ;box numberOfTweetsForUser ;  box tweetRateperSecond |]),userId)
    printfn "user created with userId %s" userId 
    userActorRefArray.[i-1]  <! (1)


while not (ExecutionShutDownFlag) do
    ignore ()

printfn "_____________________Simulation Completed : System Is Shutting Down_________________________"

printfn "||||||||||||||||||||| *******  PERFORMANCE EVLUATION *********|||||||||||||||||||||||||||||||"

printfn "Total Number of simulated Users users : %i" TotalNumberofUsers
printfn "Simulated Session Time Between Login/Logout For each user : %i" SessionTimeFOrEachUserInSeconds
printfn "successfully registered users : %i" SuccessfullyRegisteredUsersCounter
printfn "successfully logged in User : %i" SuccessfullyLoggedinUsersCounter
printfn "successfully logged out users : %i" SuccessfullyLoggedOutUsersCounter

printfn "--------------------------------------------------------------------------------------------"

printfn "Approx. Time taken for Registration of %i users : %A Milliseconds" TotalNumberofUsers registrationEndPointPerformanceTimer.ElapsedMilliseconds
printfn "Approx  Time taken for Login of %i users  : %A Milliseconds" TotalNumberofUsers LoginEndPointPerformanceTimer.ElapsedMilliseconds  
printfn "Approx. Time taken for Subscription process of %i users : %A Milliseconds" TotalNumberofUsers SubscriptionEndPointPerformanceTimer.ElapsedMilliseconds
printfn "Successful Tweets delivered %i " SuccessfulTweets 
printfn "Approx. Time taken for Logout of %i users : %A Milliseconds" TotalNumberofUsers LogoutEndPointPerformanceTimer.ElapsedMilliseconds
printfn "Approx  Time taken for 1 Hashtag query : %A Milliseconds"  HashTagQueryEndpointPointPerformanceTimer.ElapsedMilliseconds  
printfn "Approx. Time taken for 1 Mention query : %A Milliseconds"  MentionsQueryEndpointPointPerformanceTimer.ElapsedMilliseconds
printfn "Approx  Time taken for Get All User Tweets Query : %A Milliseconds"  UserTweetsQueryEndpointPointPerformanceTimer.ElapsedMilliseconds  
