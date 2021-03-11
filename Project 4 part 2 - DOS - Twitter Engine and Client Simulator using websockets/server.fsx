// ActorSayHello.fsx
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"
#r "nuget: Suave"

#r "nuget: FSharp.Json" 
open FSharp.Json
open System
open System.Net

open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open System.Collections.Generic
open System.Collections.Concurrent
open System.Text.RegularExpressions

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket





let numberOfUser = 1000
let concurrencyLevel = 16
let TotalNumberOFServerInstances = 32

//Declared types for matching cases within actor
type Trial = int
type RegisterUser = string * string     // username passowrd
type Follow = int* string * string   // int for identification fo match no use ,UserID of user to be followed , self userid 
type Tweet = int * int * string * string * string * string
type Retweet = int * int * int * int * int * string *  string * string
type HashTagQuery = int * int * int * string
type MentionQuery = int * int * int * int * string
type GetAllUserTweets = int * int * int * int * int * string  
type Login = int * int * int * string * string 
type Logout = int * int * int * int * string * string  // int for identification fo match no use 
let mutable globalWebSocket = 1

//Concurrent dictionaires for thread safety
let mutable usersTable: ConcurrentDictionary<string,string> = new ConcurrentDictionary<string,string>(concurrencyLevel, numberOfUser)
let mutable tweetsTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable feedTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable followersTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable SubscriptionTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable hashTagsTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable mentionsTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable activeUsers : List<string> = new List<string>()

let mutable webSocketsReferenceTable: ConcurrentDictionary<string,WebSocket> = new ConcurrentDictionary<string,WebSocket>(concurrencyLevel, numberOfUser)

let mutable ExecutionShutDownFlag = false
let mutable count = 0

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

type ResponseJsonObj= {
  RequestType : string;
  RequestStatus : string;
  ResponseString1 : string;
  ResponseString2 : string
  
}

let mutable responseJsonDummy : ResponseJsonObj = {RequestType = "Registration" ;RequestStatus = "100" ; ResponseString1 = "hello" ; ResponseString2 = "hi" }


let system = ActorSystem.Create("ActorSystemSupervisor")

let matchHashTagsAndMentions (inputString : string) =
    let mutable regexPattern1 = "(?:^|\s+)(?:(?<mention>@)|(?<hash>#))(?<item>\w+)(?=\s+)"
    let mutable regexPattern2 =  "\B#\w\w+"
    //let mutable regexPattern = "/^#\w+$/"
    let regexPatt1 = new Regex(regexPattern1)
    let regexPatt2 = new Regex(regexPattern2)
    let mentionMatches = regexPatt1.Matches inputString
    let hashTagMatches = regexPatt2.Matches inputString
    let mutable mentionList : List<string> = new List<string>()
    let mutable hashtagList : List<string> = new List<string>()
    for i in 0..mentionMatches.Count - 1 do
        let mutable valss : string = (mentionMatches.Item(i)).ToString().Trim()
        if(valss.[0] = '@') then
            mentionList.Add(valss)         
    for i in 0..hashTagMatches.Count - 1 do
        let mutable valss : string = (hashTagMatches.Item(i)).ToString().Trim()
        if (valss.[0] = '#') then
                hashtagList.Add(valss)
    (mentionList,hashtagList)

let mutable serverIdCounter : int = 0
let getServerID() : int =
    if (serverIdCounter >= TotalNumberOFServerInstances) then
        serverIdCounter <- 0
    serverIdCounter <- serverIdCounter + 1
    serverIdCounter

let sendResponse (response : string ,webSocketObj : WebSocket ) = 
    socket {  
           let byteResponse =
               response
               |> System.Text.Encoding.ASCII.GetBytes
               |> ByteSegment
           do! webSocketObj.send Text byteResponse true 
    }

type ServerActor() =
  inherit Actor()
  override x.OnReceive message =
     match message with
     | :? Trial as msg -> let mutable response : string = "hello from actor"
                          let mutable webSocketObj : WebSocket = webSocketsReferenceTable.Item("user1")
                          while (true) do
                            sendResponse(response , webSocketObj)
                              

     | :? RegisterUser as msg -> let (username , password) = unbox<RegisterUser> msg
                                 let mutable dummyVar = 0
                                 let mutable registrationSuccessful : bool = false
                                 let mutable usernameExistsAlready: bool = usersTable.ContainsKey(username)
                                 if(usernameExistsAlready) then
                                    // try
                                    //     x.Sender <! (false ,"An user with Username : " + username + " already exists, please use a different username" )
                                    // with 
                                    //     | _ -> x.Sender <! (false ,"An user with Username : " + username + " already exists, please use a different username" )
                                    let mutable dataString : string = "An user with Username : " + username + " already exists, please use a different username"
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "RegisterUser" ; RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson
                                 else
                                    while (not registrationSuccessful) do
                                        let mutable tweetBag : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        let mutable feedBag : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        let mutable followersBag : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        let mutable subscriptionBag : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        let mutable isUsersTableUpdated : bool = false
                                        let mutable isTweetTableUpdated : bool = false
                                        let mutable isFeedTableUpdated : bool = false
                                        let mutable isFollowersTableUpdated : bool = false
                                        let mutable isSubscriptionTableUpdated : bool = false
                                        
                                        if not (usersTable.ContainsKey(username)) then
                                           isUsersTableUpdated <- usersTable.TryAdd(username, password)
                                        else
                                           isUsersTableUpdated <- true

                                        if not (tweetsTable.ContainsKey(username)) then
                                           isTweetTableUpdated <- (tweetsTable.TryAdd(username , tweetBag)) 
                                        else
                                           isTweetTableUpdated <- true

                                        if not (feedTable.ContainsKey(username)) then
                                           isFeedTableUpdated <- (feedTable.TryAdd(username , feedBag)) 
                                        else
                                           isFeedTableUpdated <- true

                                        if not (followersTable.ContainsKey(username)) then
                                           isFollowersTableUpdated <- (followersTable.TryAdd(username, followersBag))
                                        else
                                           isFollowersTableUpdated <- true

                                        if not (SubscriptionTable.ContainsKey(username)) then
                                           isSubscriptionTableUpdated <- (SubscriptionTable.TryAdd(username, subscriptionBag))
                                        else
                                           isSubscriptionTableUpdated <- true

                                        registrationSuccessful <- (isUsersTableUpdated && isTweetTableUpdated && isFeedTableUpdated && isFollowersTableUpdated && isSubscriptionTableUpdated)

                                    let mutable dataString : string = "User with userId : " + username + " is  registered with Twitter"
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "RegisterUser" ; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson
                                    
                                 
                          

     | :? Login as msg -> let (int1, int2, int3, username , password) = unbox<Login> msg
                          let mutable doesUsernameExists: bool = usersTable.ContainsKey(username)
                          if (doesUsernameExists) then
                             if(password = (usersTable.Item(username))) then
                                activeUsers.Add(username)
                                printfn "Login Successful for userID : %s "  username
                                let mutable dataString : string = "User with userId : " + username + "Logged In Successfully"
                                let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Login"; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                let responseJson : string = Json.serialize responseJsonObj
                                x.Sender <! responseJson
                             else
                                let mutable dataString : string = "Login Failed for userId : " + username + ", Password Is Wrong "
                                let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Login"; RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                let responseJson : string = Json.serialize responseJsonObj
                                x.Sender <! responseJson
                          else
                                let mutable dataString : string = "LogIn Failed , userID : " + username + " Does Not Exist"
                                let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Login"; RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                let responseJson : string = Json.serialize responseJsonObj
                                x.Sender <! responseJson

     | :? Follow as msg -> let mutable (int1, username , followUserId ) = unbox<Follow> msg
                           if (activeUsers.Contains(username)) then
                              let mutable isUserFollowed : bool = false
                              let mutable loopBreakFlag : bool = true
                              while (not isUserFollowed) && loopBreakFlag do
                                    if(followersTable.ContainsKey(followUserId)) then
                                        let mutable followerBag : ConcurrentBag<string> = followersTable.Item(followUserId)
                                        let mutable bagContentList = ((followerBag.ToArray()) |> Array.toList)  
                                        if not (List.contains (username) (bagContentList) ) then
                                           followerBag.Add(username)
                                           isUserFollowed <- true
                                        else
                                           isUserFollowed <- true
                                    else
                                         loopBreakFlag <- false //to break the loop

                              let mutable isUserAddedTosubscriptionList : bool = false
                              let mutable loopBreakFlag2 : bool = true
                              while (not isUserAddedTosubscriptionList) && loopBreakFlag2 do
                                    if(SubscriptionTable.ContainsKey(username)) then
                                        let mutable subscriptionBag : ConcurrentBag<string> = SubscriptionTable.Item(username)
                                        let mutable bagContentListofSubscriptions = ((subscriptionBag.ToArray()) |> Array.toList)  
                                        if not (List.contains (followUserId) (bagContentListofSubscriptions) ) then
                                           subscriptionBag.Add(followUserId)
                                           isUserAddedTosubscriptionList <- true
                                        else
                                           isUserAddedTosubscriptionList <- true
                                    else
                                         loopBreakFlag <- false //to break the loop

                              if (isUserFollowed && isUserAddedTosubscriptionList) then
                                 let mutable dataString : string = "the user with userId : " + username + ",  followed user : " + followUserId 
                                 let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Follow" ; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                 let responseJson : string = Json.serialize responseJsonObj
                                 x.Sender <! responseJson
                              else
                                    let mutable dataString : string = "the user with userId : " + username + ", tried following user : " + followUserId + ", But operation failed"
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Follow" ;RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson

                           else 
                               let mutable dataString : string = "the user with userId : " + username + " not LoggedIn , In order to follow other users  Login is required"
                               let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Follow" ; RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                               let responseJson : string = Json.serialize responseJsonObj
                               x.Sender <! responseJson

                          

     | :? Logout as msg -> let (int1, int2, int3, int4, username , password) = unbox<Logout> msg
                           if (activeUsers.Contains(username)) then 
                              activeUsers.Remove(username) |> ignore
                              let mutable dataString : string =  "userID : " + username + "Logged out Successfully" 
                              let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Logout";RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                              let responseJson : string = Json.serialize responseJsonObj
                              x.Sender <! responseJson
                           else
                              let mutable dataString : string =  "Logout Failed , user : " + username + "not logged in"
                              let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Logout";RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                              let responseJson : string = Json.serialize responseJsonObj
                              x.Sender <! responseJson


     | :? Tweet as msg -> let mutable (tweetNumber , int2 , username , tweet , hashtag , mention) = unbox<Tweet> msg 
                          if (activeUsers.Contains(username)) then                              
                             let mutable tweetBagEmptyObj : ConcurrentBag<string> = new ConcurrentBag<string>()
                             let mutable feedBagEmptyObj : ConcurrentBag<string> = new ConcurrentBag<string>()

                             let mutable isTweetSuccessful : bool = false
                             let mutable isTweetTableUpdated : bool = false
                             let mutable isFeedTableUpdated : bool = false
                             let mutable isHashTagTableUpdated : bool = false
                             let mutable isMentionTableUpdated : bool = false
                             
                                //add to tweet table
                             let mutable tweetsBag : ConcurrentBag<string> = tweetsTable.Item(username)
                             tweetsBag.Add("Tweet : "+ tweet + " , Tweet Time : " + (DateTime.Now.ToString()))
                             let mutable dataString : string = "UserID : " + username + " Tweeted, from its account "  + " Tweet " + (string tweetNumber) + " : " + tweet
                             let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Tweet"; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                             let responseJson : string = Json.serialize responseJsonObj
                             x.Sender <! responseJson
                             printfn "tweeting : %s" responseJson 
                             let mutable followersBag : ConcurrentBag<string> = followersTable.Item(username)
                             let mutable followersArray : string array = followersBag.ToArray()
                             let mutable feedTweet : string = " Tweet from UserID : " + username + " ,Tweet : " + tweet
                             for i in 0 .. (followersArray.Length - 1) do
                                    //feed table of all  followers
                                    let mutable feedBag : ConcurrentBag<string> = feedTable.Item(followersArray.[i])
                                    
                                    feedBag.Add(feedTweet)
                             if (mentionsTable.ContainsKey(mention) ) then
                                    let mutable mentionsBag : ConcurrentBag<string> = mentionsTable.Item(mention)
                                    mentionsBag.Add(feedTweet)
                             else
                                    let mutable mentionBagObj : ConcurrentBag<string> = new ConcurrentBag<string>()
                                    mentionBagObj.Add(feedTweet)
                                    let mutable tryAddFlag2 : bool = false
                                    while (not tryAddFlag2) do
                                        tryAddFlag2 <-  mentionsTable.TryAdd(mention,mentionBagObj)

                             if (hashTagsTable.ContainsKey(hashtag) ) then
                                    let mutable hashTagsBag : ConcurrentBag<string> = hashTagsTable.Item(hashtag)
                                    hashTagsBag.Add(feedTweet)
                             else
                                    let mutable hashTagsBagObj : ConcurrentBag<string> = new ConcurrentBag<string>()
                                    hashTagsBagObj.Add(feedTweet)
                                    let mutable tryAddFlag3 : bool = false
                                    while (not tryAddFlag3) do
                                        tryAddFlag3 <-  hashTagsTable.TryAdd(hashtag,hashTagsBagObj)  
                                                            
     | :? Retweet as msg -> let mutable (int1,int2,int3,int4,int5, ActualTweeter , Retweeter , tweet) = unbox<Retweet> msg
                            let mutable subscriptionBag : ConcurrentBag<string> = SubscriptionTable.Item(Retweeter)
                            let mutable followedTweeters : string array = subscriptionBag.ToArray()
                            let mutable retweetfound : bool = false
                            let mutable i = 0
                            while i < (followedTweeters.Length - 1) && (not retweetfound) do
                                    if(feedTable.ContainsKey(followedTweeters.[i])) then
                                        let mutable feedBag : ConcurrentBag<string> = feedTable.Item(followedTweeters.[i])
                                        let mutable feedTweetArray : string array = feedBag.ToArray()
                                        if(feedTweetArray.Length > 0) then
                                            retweetfound < true
                                            let mutable index = System.Random().Next(feedTweetArray.Length)
                                            let mutable dataString : string = "Retweet from : " + Retweeter + ", Original : " + feedTweetArray.[index]
                                            let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Retweet"; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                            let responseJson : string = Json.serialize responseJsonObj
                                            x.Sender <! responseJson
                                    i <- i + 1
                            
                            if not (retweetfound ) then
                                        let mutable dataString : string = "Retweet By user " + Retweeter + "Actual tweet by " + ActualTweeter
                                        let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Retweet"; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                        let responseJson : string = Json.serialize responseJsonObj
                                        x.Sender <! responseJson

     | :? HashTagQuery as msg -> let mutable (int1, int2, int3, hashtag) =  unbox<HashTagQuery> msg
                                 if (hashTagsTable.ContainsKey(hashtag)) then
                                    let mutable hashTagsBag : ConcurrentBag<string> = hashTagsTable.Item(hashtag)
                                    let mutable hashTaggedTweets : string array =  hashTagsBag.ToArray()
                                    let mutable allTweetsContainingHashtag : string = ""
                                    for i in 0 .. hashTaggedTweets.Length - 1 do
                                        allTweetsContainingHashtag <- allTweetsContainingHashtag + " , " + hashTaggedTweets.[i]
                                    //x.Sender <! (true ,hashtag, allTweetsContainingHashtag)
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "HashTagQuery" ; RequestStatus = "SUCCESS" ; ResponseString1 = hashtag ; ResponseString2 = allTweetsContainingHashtag }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson
                                 else
                                    //x.Sender <! (false ,hashtag, "")
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "HashTagQuery" ; RequestStatus = "FAILED" ; ResponseString1 = hashtag ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson


     | :? MentionQuery as msg -> let mutable (int1, int2, int3, int4, mention) =  unbox<MentionQuery> msg
                                 if (mentionsTable.ContainsKey(mention)) then
                                    let mutable mentionsBag : ConcurrentBag<string> = mentionsTable.Item(mention)
                                    let mutable mentionTweets : string array =  mentionsBag.ToArray()
                                    let mutable allTweetsContainingMentions : string = ""
                                    for i in 0 .. mentionTweets.Length - 1 do
                                        allTweetsContainingMentions <- allTweetsContainingMentions + " , " + mentionTweets.[i]
                                    //x.Sender <! (true ,true, mention ,allTweetsContainingMentions)
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "MentionsQuery" ; RequestStatus = "SUCCESS" ; ResponseString1 = mention ; ResponseString2 = allTweetsContainingMentions }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson
                                 else
                                    //x.Sender <! (false , false, mention ,"")      
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "MentionsQuery" ; RequestStatus = "FAILED" ; ResponseString1 = mention ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson

     | :? GetAllUserTweets as msg -> let mutable (int1, int2, int3, int4, int5 , userId) =  unbox<GetAllUserTweets> msg 
                                     if (tweetsTable.ContainsKey(userId)) then
                                         let mutable tweetsBag : ConcurrentBag<string> = tweetsTable.Item(userId)
                                         let mutable tweetsArray : string array =  tweetsBag.ToArray()
                                         let mutable allTweetsFromUser: string = ""
                                         for i in 0 .. tweetsArray.Length - 1 do
                                            allTweetsFromUser <- allTweetsFromUser + " , " + tweetsArray.[i]
                                         //x.Sender <! (true ,true, true, userId , allTweetsFromUser)
                                         let mutable responseJsonObj : ResponseJsonObj = {RequestType = "GetUsersTweetQuery" ; RequestStatus = "SUCCESS" ; ResponseString1 = userId ; ResponseString2 = allTweetsFromUser }
                                         let responseJson : string = Json.serialize responseJsonObj
                                         x.Sender <! responseJson
                                     else
                                         //x.Sender <! (true ,true, true, userId , "")
                                         let mutable responseJsonObj : ResponseJsonObj = {RequestType = "GetUsersTweetQuery" ; RequestStatus = "FAILED" ; ResponseString1 = userId ; ResponseString2 = "none" }
                                         let responseJson : string = Json.serialize responseJsonObj
                                         x.Sender <! responseJson
                                                                          

     | _ -> failwith "wrong message format for child node"

let serverActorRefArray : IActorRef array = Array.zeroCreate (TotalNumberOFServerInstances+1)
for i in 1 .. TotalNumberOFServerInstances do
    let mutable serverId : string = "server" + string(i)
    serverActorRefArray.[i - 1] <- system.ActorOf(Props(typedefof<ServerActor>),serverId)
    printfn "server created with serverId %s" serverId

// while not ExecutionShutDownFlag do
//     ignore ()

let ws (webSocket : WebSocket) (context: HttpContext) =
  socket {
    // if `loop` is set to false, the server will stop receiving messages
    let mutable loop = true

    while loop do
      // the server will wait for a message to be received without blocking the thread
      let! msg = webSocket.read()
      

      match msg with

      | (Text, data, true) ->
        // the message can be converted to a string
        let requestMessage = UTF8.toString data
        printfn "printing response"

        let mutable serverId : int = getServerID()
        let mutable serverRef = serverActorRefArray.[serverId - 1]
        let mutable isWebSocketSavedInMap : bool = false
        let mutable task = Unchecked.defaultof<_>
        let mutable requestJsonobj : RequestJsonObj =  Json.deserialize<RequestJsonObj>(requestMessage)
        if(requestJsonobj.RequestType = "RegisterUser") then
            if not (webSocketsReferenceTable.ContainsKey(requestJsonobj.UserId)) then
                let mutable isWebSocketSavedInMap : bool = false
                while not (isWebSocketSavedInMap ) do
                    isWebSocketSavedInMap <- webSocketsReferenceTable.TryAdd(requestJsonobj.UserId, webSocket)
        if(requestJsonobj.RequestType = "RegisterUser") then
            //serverRef <! (username , password)
            task <- serverRef <? (requestJsonobj.UserId , requestJsonobj.Password)
        if(requestJsonobj.RequestType = "Login") then
            //serverRef <! (1, 1, 1 , username , password)
            task <- serverRef <? (1,1,1,requestJsonobj.UserId , requestJsonobj.Password)
        if(requestJsonobj.RequestType = "Follow") then
            //serverRef <! (1, username , susbcritionList.Item(i))
            task <- serverRef <? (1,requestJsonobj.UserId , requestJsonobj.FollowUserId)
        if(requestJsonobj.RequestType = "Tweet") then
            //serverRef <! (tweetNumber, 1, username , tweet)
            printfn("tweeting now")
            task <- serverRef <? (requestJsonobj.TweetNumber, 1, requestJsonobj.UserId , requestJsonobj.Tweet,requestJsonobj.HashTag ,requestJsonobj.Mention)
        if(requestJsonobj.RequestType = "Retweet") then
            //serverRef <! (1, 1, 1, 1, 1, subscriptionUserName, username ,tweet)
            task <- serverRef <? (1, 1, 1, 1, 1, requestJsonobj.SubscriptionUserName, requestJsonobj.UserId ,requestJsonobj.Tweet)
        if(requestJsonobj.RequestType = "HashTagQuery") then
            //serverRef <! (1, 1, 1, hashtag)  
            task <- serverRef <? (1, 1, 1, requestJsonobj.HashTag)  
        if(requestJsonobj.RequestType = "MentionsQuery") then
            //serverRef <!  (1, 1, 1, 1, mention) 
            task <- serverRef <?  (1, 1, 1, 1, requestJsonobj.Mention) 
        if(requestJsonobj.RequestType = "GetUsersTweetQuery") then
            //serverRef <! (1, 1, 1, 1, 1 , userId)
            task <-  serverRef <? (1, 1, 1, 1, 1 , requestJsonobj.UserId)
        if(requestJsonobj.RequestType = "Logout") then
            //serverRef <! (1, 1, 1, 1, username , password) 
            task <- serverRef <? (1,1,1,1,requestJsonobj.UserId , requestJsonobj.Password)

        
        let responseActor = Async.RunSynchronously (task, 30000)
        let mutable response = sprintf "%s" responseActor
        printfn "response form actor is : %s" response
        if(response = Unchecked.defaultof<_> || response.Length = 0 ) then
            let mutable responseJsonObj : ResponseJsonObj = {RequestType = requestJsonobj.RequestType ; RequestStatus = "FAILED" ; ResponseString1 = "Server failed to serve the request because of high load" ; ResponseString2 = "none" }
            printfn "changing reponse because request failed "
            response <- Json.serialize responseJsonObj
        printfn "getuserstweetsquery response ws is : %s" response


        // the response needs to be converted to a ByteSegment
        let byteResponse =
          response
          |> System.Text.Encoding.ASCII.GetBytes
          |> ByteSegment

        // the `send` function sends a message back to the client
        do! webSocket.send Text byteResponse true

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true

        // after sending a Close message, stop the loop
        loop <- false

      | _ -> ()
    }

/// An example of explictly fetching websocket errors and handling them in your codebase.
let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) = 
   
   let exampleDisposableResource = { new IDisposable with member __.Dispose() = printfn "Resource needed by websocket connection disposed" }
   let websocketWorkflow = ws webSocket context
   
   async {
    let! successOrError = websocketWorkflow
    match successOrError with
    // Success case
    | Choice1Of2() -> ()
    // Error case
    | Choice2Of2(error) ->
        // Example error handling logic here
        printfn "Error: [%A]" error
        exampleDisposableResource.Dispose()
        
    return successOrError
   }

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") ws
    path "/websocketWithError" >=> handShake wsWithErrorHandling
    GET >=> choose [ path "/" >=> OK "index" ]
    NOT_FOUND "Found no handlers." ]


startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
printfn "starting server"


