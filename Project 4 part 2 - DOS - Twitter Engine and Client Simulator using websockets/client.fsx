open System.Net.WebSockets
// ActorSayHello.fsx
#time "on"
#r "csProject.dll"

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"
#r "nuget: FSharp.Json" 
open FSharp.Json


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
type SubscriptionFeedTweet = int * int * int * int * int * int * string * string * WebsocketService * ClientWebSocket
type SubscriptionFeedRetweet = int * int * int * int * int * int * int * string * string
type Retweet = float * string * string * WebsocketService * ClientWebSocket
type RetweetStatus = int * float * float * string
type HashTagQuery = float * string
type MentionsQuery =   float  * float * string
type GetUsersTweetQuery = float  * float * float * string
type HashTagQueryResponse = bool * string * string 
type MentionsQueryResponse = bool * bool * string * string 
type GetUsersTweetResponse = bool * bool * bool * string * string
type FollowAnotherUser = int * string * string   
type MessageFromServer = string

type ReceieveTweet = WebsocketService * ClientWebSocket * CancellationToken
type SetReceiveFlagOff = bool



let mutable ExecutionShutDownFlag = false
let mutable query1Flag : bool = false
let mutable query2Flag : bool = false
let mutable query3Flag : bool = false
let mutable serverIdCounter : int = 0
let mutable TotalNumberofUsers : int =20
let mutable TotalNumberOfTweets : int = 100
let mutable SessionTimeFOrEachUserInSeconds : int = 60
let TotalNumberOFServerInstances = 32

let args : string array = fsi.CommandLineArgs |>  Array.tail//
// //fetch the first argument - total number of nodes in the system
TotalNumberofUsers <- args.[0] |> int
SessionTimeFOrEachUserInSeconds <-  args.[1] |> int

let mutable TotalRequests : int = 0
let mutable TotalRegistrationRequests : int = 0
let mutable SuccessfulRegistrations : int = 0
let mutable TotalLoginRequests : int = 0
let mutable SuccessfulLoginRequests : int = 0
let mutable TotalFollowRequests : int = 0
let mutable SuccessfulFollowRequests : int = 0
let mutable TotalTweetRequests : int = 0
let mutable SuccessfulTweetRequests : int = 0
let mutable TotalLogoutRequests: int = 0
let mutable SuccessfulLogoutRequests: int = 0
let mutable TotalDataQueryRequests : int = 0
let mutable SuccessfulDataQueryRequests : int = 0


//flags to turn the stopwatches on only once
let mutable LogoutWatchFlag = 1



let mutable SuccessfullyRegisteredUsersCounter : int  = 0
let mutable SuccessfullyLoggedinUsersCounter : int  = 0
let mutable SuccessfullyLoggedOutUsersCounter : int  = 0
let mutable SuccessfullySubscriptionUsersCounter : int  = 0
let mutable SuccessfulTweets : int = 0

let hashTags = [| "#life"; "#politics"; "#art" ; "#electionNews" ; "#influencer" ; "#marketting";"#realestate" ; "#crypto" ; "#womenshistorymonth" ; "#pets"|]
let mentions : List<string> = new List<string>() 

let mutable usedHashTag : string = ""
let mutable usedMention : string = ""

type RequestJsonObj= {
  RequestType : string;
  UserId : string;
  Password : string;
  TweetNumber : int
  Tweet : string;
  FollowUserId : string;
  SubscriptionUserName : string;
  Mention : string;
  HashTag : string
}

let mutable requestJsonDummy : RequestJsonObj = {RequestType = "none" ; UserId = "none" ; Password = "none" ; TweetNumber = 100; Tweet = "none" ; FollowUserId = "none"; SubscriptionUserName = "none" ; Mention = "none"; HashTag = "none"}

type ResponseJsonObj= {
  RequestType : string;
  RequestStatus : string;
  ResponseString1 : string;
  ResponseString2 : string
  
}


let mutable responseJsonDummy : ResponseJsonObj = {RequestType = "none" ;RequestStatus = "100" ; ResponseString1 = "none" ; ResponseString2 = "none" }

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
        printfn "3"

//contains userId as keys  and list of other users which that userId will follow on twitterengine (claculated with zipf distribution)
let mutable ZipFDistributedSubscriptionMap : Dictionary<string, List<string>> = new Dictionary<string, List<string>>() 

// contains userIds as key and number of tweets , this userId can make in a session , (claculated with zipf distribution)
let mutable ZipFDistributedTweetMap : Dictionary<string, int> = new Dictionary<string, int>()

let prepareZipFDistributionForTweetsAndSubscribers(totalNumberofUsers : int , totalNumberOfTweets : int) =   
    let mutable userIdArray : string array = Array.zeroCreate TotalNumberofUsers
    printfn "1"
    for counter in 1 .. TotalNumberofUsers do
        let mutable userId : string = "user" + string(counter)
        userIdArray.[counter - 1] <- userId 
        let mutable subscriptionList = new List<string>()
        ZipFDistributedSubscriptionMap.Add(userId,subscriptionList)
        printfn "list added : %i" counter
    shuffleArray(userIdArray)
    printfn "length of user Id array : %i " userIdArray.Length
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
        //shuffleArray(userIdArray)
        ZipFDistributedTweetMap.Add(userId , numberOfTweetsByUser)
        printfn "4"
        //add current user to subscription List of all randomly selected other users (count will be equal to numberOfSubscribersFollowingUser), which will follow current user
        let mutable i : int = 1
        //System.Random().Next(i+1,)
        printfn "numberOfSubscribersFollowingUser : %i" numberOfSubscribersFollowingUser
        printfn "numberOfTweetsByUser : %i" numberOfTweetsByUser

        let mutable loopcounter = 1
        let mutable whileFlag = true
        while i <= numberOfSubscribersFollowingUser && whileFlag do 
            loopcounter <- loopcounter + 1
            if(userIdArray.[i-1] <> userId)  then
               let mutable subscriptionListOfUser : List<string> = ZipFDistributedSubscriptionMap.Item(userIdArray.[i-1])
               subscriptionListOfUser.Add(userId)
               i <- i + 1
               printfn "5"
            if (loopcounter = userIdArray.Length) then
                whileFlag <- false

//prepare zipf distribution for users followeres and tweets
prepareZipFDistributionForTweetsAndSubscribers(TotalNumberofUsers , TotalNumberOfTweets)

let system = ActorSystem.Create("ActorSystemUser")

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

  
 
  let mutable cancellationTokenSource = new CancellationTokenSource(new TimeSpan(1, 1, 0, 0));
  let mutable tokenVal = cancellationTokenSource.Token;

  let mutable webscoketApiURL : string = "ws://127.0.0.1:8080/websocket";
  let mutable webSocketClient = WebsocketService()
  let mutable socketObj = new ClientWebSocket()
   
  override x.OnReceive message =
     match message with
     | :? RegisterUserOnTwitterApp as msg -> let mutable requestJson : RequestJsonObj = {RequestType = "RegisterUser" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none"; SubscriptionUserName = "none" ; Mention = "none"; HashTag = "none"}
                                             let data : string = Json.serialize requestJson
                                             
                                             TotalRegistrationRequests <- TotalRegistrationRequests + 1
                                             webSocketClient.Connect(socketObj, webscoketApiURL, tokenVal).Wait(tokenVal)
                                             webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                                             let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                                             printfn "responseJsonString : %s" responseJsonString
                                             let mutable responseJson : ResponseJsonObj = Unchecked.defaultof<_>
                                             try
                                                responseJson <- Json.deserialize<ResponseJsonObj>(responseJsonString)
                                                printf ""
                                             with
                                                | _ -> printfn "Registration ResponseJson Deserialization Failed"
                                             if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "RegisterUser" && responseJson.RequestStatus = "SUCCESS") then
                                                SuccessfulRegistrations <- SuccessfulRegistrations + 1
                                                x.Self <! (true,(responseJson.ResponseString1))
                                             else
                                                x.Self <! (false,("registration request failed for user :  " + username))

                                             
                                             

     | :? RegistrationStatus as msg ->  let mutable (userRegisterationStatus , serverMessage) = unbox<RegistrationStatus> msg    
                                        if (userRegisterationStatus) then
                                            isRegistrationSuccessful <- true 
                                            printfn "Server Response : %s " serverMessage
                                            x.Self <! (1,1)
                                        else      
                                            printfn "Server Response : %s " serverMessage
                                         
     | :? Login as msg -> if (isRegistrationSuccessful) then
                              let mutable requestJson : RequestJsonObj = {RequestType = "Login" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none"; SubscriptionUserName = "none" ; Mention = "none"; HashTag = "none"}
                              let data : string = Json.serialize requestJson
                              TotalLoginRequests <- TotalLoginRequests + 1
                              webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                              let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                              let mutable responseJson  =  Unchecked.defaultof<_>
                              try
                                  responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                              with 
                                  | _ -> printfn "Login ResponseJson Deserialization Failed"
                              if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "Login" && responseJson.RequestStatus = "SUCCESS") then
                                    SuccessfulLoginRequests <- SuccessfulLoginRequests + 1
                                    x.Self <! (true,true,(responseJson.ResponseString1))
                              else
                                    x.Self <! (false,false,("Login Failed for User : " + username))

                          else
                              printfn "User with userId : %s can not login becuase user registration was unsuccessful" username
                   
     | :? LoginStatus as msg -> let mutable (userLoginStatus ,arg2, serverMessage) = unbox<LoginStatus> msg    
                                if (userLoginStatus) then
                                    isLoginSuccessful <- true 
                                    printfn "Server Response : %s " serverMessage
                                    // start subscription
                                    x.Self <! (1,1,1,1,1)
                                    // start session
                                    //x.Self <! (1,1,1,1)
                                else      
                                    printfn "Server Response : %s " serverMessage    


     | :? subscription as msg -> if (subscriptionCount = 0) then
                                    TotalFollowRequests <- TotalFollowRequests + 1
                                   // start session
                                    x.Self <! (1,1,1,1)
                                 else
                                     for i in 0 .. (susbcritionList.Count - 1) do       
                                        let mutable requestJson : RequestJsonObj = {RequestType = "Follow" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = susbcritionList.Item(i) ; SubscriptionUserName = "none" ; Mention = "none"; HashTag = "none"}
                                        let data : string = Json.serialize requestJson
                                        webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                                        let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                                        let mutable responseJson  =  Unchecked.defaultof<_>
                                        try
                                              responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                        with 
                                              | _ -> printfn "Subscription request ResponseJson Deserialization Failed"
                                        if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "Follow" && responseJson.RequestStatus = "SUCCESS") then
                                               SuccessfulFollowRequests <- SuccessfulFollowRequests + 1
                                               x.Self <! (true,true,true,true,(responseJson.ResponseString1))
                                        else
                                               x.Self <! (false,false,false,false,("Follow Reuqest made by "+username + " to follow " + susbcritionList.Item(i) + " failed"))                         
                          
     | :? session as msg -> let mutable sessionLogOutTime : DateTime = (DateTime.Now).AddSeconds(float sessionTime)
                            // start tweeting
                            x.Self <! (1,1,1,1,1,1)
                            //start reception of feed
                            //keep session alive for given session duration
                            while (DateTime.Now < (sessionLogOutTime) ) do
                               printf ""
                            logOutFlag <- true
                    

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
                                 let hashtagRandomSelect = System.Random().Next(10)
                                 let mentionRandomSelect = System.Random().Next(mentions.Count)
                                 usedHashTag <- hashTags.[hashtagRandomSelect]
                                 usedMention <- mentions.Item(mentionRandomSelect)
                                 
                                 let mutable tweet : string = "I am Tweeting" + " " + mentions.Item(mentionRandomSelect) + " " + hashTags.[hashtagRandomSelect]
                                 let mutable requestJson : RequestJsonObj = {RequestType = "Tweet" ; UserId = username ; Password = password ; TweetNumber = tweetNumber; Tweet = tweet ; FollowUserId = "none" ; SubscriptionUserName = "none" ; Mention = usedMention ; HashTag = usedHashTag}
                                 let data : string = Json.serialize requestJson
                                 //webSocketClient.Connect(socketObj, connection, tokenVal)
                                 TotalTweetRequests <- TotalTweetRequests + 1
                                 webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                                 let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                                 let mutable responseJson  =  Unchecked.defaultof<_>
                                 try
                                        responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                 with 
                                        | _ -> printfn "tweet ResponseJson Deserialization Failed" 
                                               responseJson <- {RequestType = "Tweet" ; RequestStatus = "FAILED" ; ResponseString1 = "request failed" ; ResponseString2 = "Reason : high load" }
                                 if(responseJson.RequestType = "Tweet" && responseJson.RequestStatus = "SUCCESS") then
                                             SuccessfulTweetRequests <- SuccessfulTweetRequests + 1
                                             printfn "Server respons : %s" responseJson.ResponseString1
                                 else 
                                             printfn "Server response : tweeting operation failed because of high load"                                 
                                 tweetsCounter <- tweetsCounter + 1
                                 tweetNumber <- tweetNumber + 1  
                          x.Self <! (1.5 , "none","none",webSocketClient ,socketObj)

                                   
     | :? subscriptionStatus as msg ->  let mutable (userSubscriptionStatus , arg2, agr3, arg4, serverMessage) = unbox<subscriptionStatus> msg    
                                        if (userSubscriptionStatus) then
                                            isSubscriptionSuccessful <- true
                                            printfn "Server Response : %s " serverMessage
                                            successfulSubscriptionCounter <- successfulSubscriptionCounter + 1
                                            if (successfulSubscriptionCounter = subscriptionCount) then
                                                //start session
                                                x.Self <! (1,1,1,1)
                                        else      
                                            printfn "Server Response : %s " serverMessage     


     | :? TweetStatus as msg -> let mutable (int1, float1 , serverResponse) = unbox<TweetStatus> msg                          
                                printfn "%s" serverResponse
                                


     | :? SubscriptionFeedTweet as msg ->let mutable (int1,int2,int3,int4,int5,int6, subscriptionUserName , tweet, webSocketClient ,socketObj) = unbox<SubscriptionFeedTweet> msg
                                         let randomDecisionForRetweet = System.Random().Next(1,5)
                                         let responseStr : string = "Subscription Feed of User : " + username + " --> " + tweet
                                         printfn "%s" responseStr                       
                                         if(randomDecisionForRetweet = 2) then
                                            TotalTweetRequests <- TotalTweetRequests + 1
                                            x.Self <! (1.5 , subscriptionUserName,tweet,webSocketClient ,socketObj)
                                         else
                                            printf ""

     | :? SubscriptionFeedRetweet as msg -> let mutable (int1,int2,int3,int4,int5,int6,int7, subscriptionUserName , tweet) = unbox<SubscriptionFeedRetweet> msg
                                            let responseStr : string = "Subscription Feed of User : " + username + "  -->  " + tweet    
                                            printfn "%s" responseStr                       
                                         

     | :? Retweet as msg -> let mutable (float1,subscriptionUserName , tweet,webSocketClient ,socketObj) = unbox<Retweet> msg
                            TotalTweetRequests <- TotalTweetRequests + 1
                            let mutable requestJson : RequestJsonObj = {RequestType = "Retweet" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = tweet ; FollowUserId = "none" ; SubscriptionUserName = subscriptionUserName ; Mention = "none"; HashTag = "none"}
                            let data : string = Json.serialize requestJson
                            TotalDataQueryRequests <- TotalDataQueryRequests + 1
                            SuccessfulDataQueryRequests <- SuccessfulDataQueryRequests + 1
                            webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                            let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                            let mutable responseJson  =  Unchecked.defaultof<_>
                            try
                                   responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                            with 
                                   | _ -> printfn "retweet ResponseJson Deserialization Failed" 
                                          responseJson <- {RequestType = "Tweet" ; RequestStatus = "FAILED" ; ResponseString1 = "request failed" ; ResponseString2 = "Reason : high load" }
                            if(responseJson.RequestType = "Retweet" && responseJson.RequestStatus = "SUCCESS") then
                                    SuccessfulTweetRequests <- SuccessfulTweetRequests + 1
                                    printfn "Server respons : %s" responseJson.ResponseString1
                            else 
                                    printfn "Server response : Retweeting operation failed because of high load"   
                            x.Self <! (1,1,1)  
                                                        
                                                                                       

     | :? RetweetStatus as msg -> let mutable (int1, float1 , float2, serverResponse) = unbox<RetweetStatus> msg                          
                                  printfn "%s" serverResponse

     | :? HashTagQuery as msg ->let mutable (float1 , hashtag) = unbox<HashTagQuery> msg 
                                TotalDataQueryRequests <- TotalDataQueryRequests + 1
                                let mutable requestJson : RequestJsonObj = {RequestType = "HashTagQuery" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none" ; SubscriptionUserName = "none" ; Mention = "none"; HashTag = hashtag}
                                let data : string = Json.serialize requestJson
                                webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                                let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                                let mutable responseJson  =  Unchecked.defaultof<_>
                                try
                                    responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                with 
                                    | _ -> printfn "hashtag query request ResponseJson Deserialization Failed"
                                if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "HashTagQuery" && responseJson.RequestStatus = "SUCCESS") then
                                    SuccessfulDataQueryRequests <- SuccessfulDataQueryRequests + 1
                                    x.Self <! (true,hashtag,(responseJson.ResponseString1))
                                else
                                    x.Self <! (false,hashtag,("hashtag query Reuqest made by "+ username + " for hashtag :  " + hashtag + " failed")) 
                      

     | :? MentionsQuery as msg -> let mutable (float1 , float2, mention) = unbox<MentionsQuery> msg
                                  TotalDataQueryRequests <- TotalDataQueryRequests + 1
                                  let mutable requestJson : RequestJsonObj = {RequestType = "HashTagQuery" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none" ; SubscriptionUserName = "none" ; Mention = mention; HashTag = "none"}
                                  let data : string = Json.serialize requestJson
                                  webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                                  let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                                  let mutable responseJson  =  Unchecked.defaultof<_>
                                  try
                                     responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                  with 
                                     | _ -> printfn "mention query request ResponseJson Deserialization Failed"
                                  if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "HashTagQuery" && responseJson.RequestStatus = "SUCCESS") then
                                      SuccessfulDataQueryRequests <- SuccessfulDataQueryRequests + 1
                                      x.Self <! (true,true,mention,(responseJson.ResponseString1))
                                  else
                                      x.Self <! (false,false,mention,("mention query Reuqest made by "+ username + " for mention :  " + mention + " failed")) 
                                 
                                     

     | :? GetUsersTweetQuery as msg ->  let mutable (float1 , float2, float3, userId) = unbox<GetUsersTweetQuery> msg
                                        TotalDataQueryRequests <- TotalDataQueryRequests + 1
                                        let mutable requestJson : RequestJsonObj = { RequestType = "GetUsersTweetQuery" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none" ; SubscriptionUserName = userId ; Mention = "none" ; HashTag = "none"}
                                        let data : string = Json.serialize requestJson
                                        webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                                        let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                                        let mutable responseJson  =  Unchecked.defaultof<_>
                                        try
                                            responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                        with 
                                            | _ -> printfn "getusertweets query query request ResponseJson Deserialization Failed"
                                        if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "HashTagQuery" && responseJson.RequestStatus = "SUCCESS") then
                                            SuccessfulDataQueryRequests <- SuccessfulDataQueryRequests + 1
                                            x.Self <! (true,true,true,userId,(responseJson.ResponseString1))
                                        else
                                            x.Self <! (false,false,false, userId,("getusertweets query Reuqest made by "+ username + " for userid :  " + userId + " failed"))
                

     | :? HashTagQueryResponse as msg -> let mutable (queryBoolStatus , hashtag , responseString) = unbox<HashTagQueryResponse> msg 
                                         if queryBoolStatus then
                                             printfn "Server Response : All Tweets Containng Hashtag : %s are : %s" hashtag responseString
                                         else
                                            printfn "Server Response : No Tweet Found which Contains Hashtag : %s " hashtag 
                                         query1Flag <- true

     | :? MentionsQueryResponse as msg -> let mutable (queryBoolStatus , bool2, mention , responseString) = unbox<MentionsQueryResponse> msg 
                                          if queryBoolStatus then
                                              printfn "Server Response : All Tweets Containng mention : %s are : %s" mention responseString
                                          else
                                              printfn "Server Response : No Tweet Found which Contains mention : %s " mention
                                          query2Flag <- true

     | :? GetUsersTweetResponse as msg -> let mutable (queryBoolStatus ,bool2,bool3, username , responseString) = unbox<GetUsersTweetResponse> msg 
                                          if queryBoolStatus then
                                              printfn "Server Response : All Tweets from User with UserId: %s are  :  %s" username responseString
                                          else
                                              printfn "Server Response : No Tweet Found from User with UserId: %s" username    
                                          query3Flag <- true                                   

     | :? Logout as msg -> if(LogoutWatchFlag = 1) then   
                                  x.Self <! (1.7 , usedHashTag)
                                  x.Self <! (1.7 ,1.4, usedMention)
                                  x.Self <! (1.7 ,1.5 ,2.3, "user6")   
                                  LogoutWatchFlag <- 0 
                                  let timer2 = new Timers.Timer(float (SessionTimeFOrEachUserInSeconds * 1000))
                                  let event2 = Async.AwaitEvent (timer2.Elapsed) |> Async.Ignore
                                  timer2.Start()
                                  Async.RunSynchronously event2
                                
                           SuccessfullyLoggedOutUsersCounter <- SuccessfullyLoggedOutUsersCounter + 1
                           TotalLogoutRequests <- TotalLogoutRequests + 1
                           let mutable requestJson : RequestJsonObj = { RequestType = "Logout" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none" ; SubscriptionUserName = "none"  ; Mention = "none" ; HashTag = "none"}
                           let data : string = Json.serialize requestJson
                           webSocketClient.Send(socketObj, data , tokenVal).Wait(tokenVal)
                           let responseJsonString : string = (webSocketClient.Receive(socketObj, tokenVal)).Result
                           let mutable responseJson  =  Unchecked.defaultof<_>
                           try
                                   responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                           with 
                                  | _ -> printfn "logout request ResponseJson Deserialization Failed"
                           if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "Logout" && responseJson.RequestStatus = "SUCCESS") then
                               SuccessfulLogoutRequests <- SuccessfulLogoutRequests + 1
                               x.Self <! (true,true,true,(responseJson.ResponseString1))
                           else
                                x.Self <! (false,false,false,("logout Reuqest made by user : "+ username + " failed"))   

                            
     
     | :? LogoutStatus as msg -> let mutable (userLogoutStatus , arg2, arg3, serverMessage) = unbox<LogoutStatus> msg       
                                 printfn "Server Response : %s " serverMessage 
                                                                       
                                       
                                 

     | :? MessageFromServer as msg -> printfn "%s" msg

     | _ -> failwith "wrong message format for child node"



let userActorRefArray : IActorRef array = Array.zeroCreate TotalNumberofUsers
let tweetReceiverRefArray : IActorRef array = Array.zeroCreate TotalNumberofUsers
printfn "hello............."
//Create and simulate user and start their registration
for i in 1 .. TotalNumberofUsers do
    let mutable userId : string = "user" + string(i)
    let mutable tweetReceiverId : string = "tweetreceiver" + string(i)
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
    //tweetReceiverRefArray.[i-1] <- system.ActorOf(Props(typedefof<TweetFeedReceiverActor>),tweetReceiverId)
    userActorRefArray.[i-1]  <! (1)


let timer = new Timers.Timer(float (SessionTimeFOrEachUserInSeconds * 1000))
let event = Async.AwaitEvent (timer.Elapsed) |> Async.Ignore
timer.Start()
Async.RunSynchronously event
ExecutionShutDownFlag <- true

let timer2 = new Timers.Timer(float (SessionTimeFOrEachUserInSeconds * 1000))
let event2 = Async.AwaitEvent (timer2.Elapsed) |> Async.Ignore
timer2.Start()
Async.RunSynchronously event2
printfn "Session completed"


while not (ExecutionShutDownFlag) do
     ignore ()

system.Terminate()


printfn "_____________________Simulation Completed : System Is Shutting Down_________________________"

printfn "||||||||||||||||||||| *******  PERFORMANCE EVLUATION *********|||||||||||||||||||||||||||||||"

printfn "Total Number of simulated Users : %i" TotalNumberofUsers
printfn "Simulated Session Time Between Login/Logout For each user : %i" SessionTimeFOrEachUserInSeconds

printfn "Total Number of Registration request made : %i" TotalRegistrationRequests
printfn "Total Number of Login request made : %i" TotalLoginRequests
printfn "Total Number of Follow request made : %i" TotalFollowRequests
printfn "Total Number of Tweet/Retweet request made : %i" TotalTweetRequests
printfn "Total Number of Logout request made : %i" TotalLogoutRequests
printfn "Total Number of dataQueryRequests(Hashtag/Mention/GetUserstweets) request made : %i" TotalDataQueryRequests

printfn "Successful Registration requests made : %i" SuccessfulRegistrations
printfn "Successful Login request made : %i" SuccessfulLoginRequests
printfn "Successful Follow request made : %i" SuccessfulFollowRequests
printfn "Successful Tweet/Retweet request made : %i" SuccessfulTweetRequests
printfn "Successful Logout request made : %i" SuccessfulLogoutRequests
printfn "Successful dataQueryRequests(Hashtag/Mention/GetUserstweets) request made : %i" SuccessfulDataQueryRequests

printfn "failed Registration requests made : %i" (TotalRegistrationRequests - SuccessfulRegistrations)
printfn "failed Login request made : %i" (TotalLoginRequests - SuccessfulLoginRequests )
printfn "failed Follow request made : %i" (TotalFollowRequests - SuccessfulFollowRequests)
printfn "failed Tweet/Retweet request made : %i" (TotalTweetRequests - SuccessfulTweetRequests)
printfn "failed Logout request made : %i" (TotalLogoutRequests - SuccessfulLogoutRequests)
printfn "failed dataQueryRequests(Hashtag/Mention/GetUserstweets) request made : %i" (TotalDataQueryRequests - SuccessfulDataQueryRequests)






