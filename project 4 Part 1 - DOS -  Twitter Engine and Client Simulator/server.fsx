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
open System.Text.RegularExpressions


let numberOfUser = 2000
let concurrencyLevel = 16
let TotalNumberOFServerInstances = 128

//Declared types for matching cases within actor
type RegisterUser = string * string     // username passowrd
type Follow = int* string * string   // int for identification fo match no use ,UserID of user to be followed , self userid 
type Tweet = int * int * string * string 
type Retweet = int * int * int * int * int * string *  string * string
type HashTagQuery = int * int * int * string
type MentionQuery = int * int * int * int * string
type GetAllUserTweets = int * int * int * int * int * string  
type Login = int * int * int * string * string 
type Logout = int * int * int * int * string * string  // int for identification fo match no use 


//Concurrent dictionaires for thread safety
let mutable usersTable: ConcurrentDictionary<string,string> = new ConcurrentDictionary<string,string>(concurrencyLevel, numberOfUser)
let mutable tweetsTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable feedTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable followersTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable SubscriptionTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable hashTagsTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable mentionsTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(concurrencyLevel, numberOfUser)
let mutable activeUsers : List<string> = new List<string>()

let mutable ExecutionShutDownFlag = false
let mutable count = 0

let config =
    ConfigurationFactory.ParseString(
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = ""localhost""
                port = 9000
            }
        }")

let system = System.create "ServerActorSystem" config

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

type ServerActor() =
  inherit Actor()
  override x.OnReceive message =
     match message with
     | :? RegisterUser as msg -> let (username , password) = unbox<RegisterUser> msg
                                 let mutable dummyVar = 0
                                 let mutable registrationSuccessful : bool = false
                                 let mutable usernameExistsAlready: bool = usersTable.ContainsKey(username)
                                 if(usernameExistsAlready) then
                                    try
                                        x.Sender <! (false ,"An user with Username : " + username + " already exists, please use a different username" )
                                    with 
                                        | _ -> x.Sender <! (false ,"An user with Username : " + username + " already exists, please use a different username" )
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
    
                                    try
                                        x.Sender <! (true , "User with userId : " + username + " is  registered with Twitter" )
                                    with
                                        | _ -> x.Sender <! (true , "User with userId : " + username + " is registered with Twitter" )

                                 
                          

     | :? Login as msg -> let (int1, int2, int3, username , password) = unbox<Login> msg
                          let mutable doesUsernameExists: bool = usersTable.ContainsKey(username)
                          if (doesUsernameExists) then
                             if(password = (usersTable.Item(username))) then
                                activeUsers.Add(username)
                                try
                                    printfn "Login Successful for userID : %s "  username
                                    x.Sender <! (true, true , "User with userId : " + username + "Logged In Successfully" )
                                with
                                    | _ -> x.Sender <! (true, true , "User with userId : " + username + " Logged In Successfully" )
                             else
                                try
                                    x.Sender <! (false, false, "Login Failed for userId : " + username + ", Password Is Wrong ")
                                with
                                    | _ ->  x.Sender <! (false, false, "Login Failed for userId : " + username + ", Password Is Wrong ")
                          else
                                try
                                    x.Sender <! (false, false, "LogIn Failed , userID : " + username + " Does Not Exist")
                                with
                                    | _ -> x.Sender <! (false, false, "LogIn Failed , userID : " + username + " Does Not Exist")

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
                                 try
                                    x.Sender <! (true, true, true ,true, "the user with userId : " + username + ",  followed user : " + followUserId )
                                 with
                                    | _ -> x.Sender <! (true, true, true ,true, "the user with userId : " + username + ",  followed user : " + followUserId)  
                              else
                                    try
                                       x.Sender <! (false, false, false ,false, "the user with userId : " + username + ", tried following user : " + followUserId + ", But operation failed")
                                    with
                                       | _ -> x.Sender <! (false, false, false ,false, "the user with userId : " + username + ", tried following user : " + followUserId + ", But operation failed")

                           else
                               try
                                    x.Sender <! (false, false, false ,false, "the user with userId : " + username + " not LoggedIn , In order to follow other users  Login is required")
                               with
                                    | _ -> x.Sender <! (false, false, false ,false,"the user with userId : " + username + " not LoggedIn , In order to follow other users Login is required") 

                          

     | :? Logout as msg -> let (int1, int2, int3, int4, username , password) = unbox<Logout> msg
                           if (activeUsers.Contains(username)) then 
                              activeUsers.Remove(username) |> ignore
                              try
                                  for i in 0 .. activeUsers.Count - 1 do
                                       printfn "%s" (activeUsers.Item(i))
                                  x.Sender <! (true, true ,true, "userID : " + username + "Logged out Successfully" )
                              with
                                  | _ -> x.Sender <! (true, true ,true, "userID : " + username + "Logged out Successfully" )
                           else
                              try
                                  x.Sender <! ( false, false, false, "LogOut Failed , user : " + username + "not logged in")   
                              with
                                  | _ -> x.Sender <! ( false, false, false, "LogOut Failed , user : " + username + "not logged in")   


     | :? Tweet as msg -> let mutable (tweetNumber , int2 , username , tweet) = unbox<Tweet> msg 
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
                             x.Sender <! (1, 1.5 , "Server Response , UserID : " + username + " Tweeted, "  + " Tweet " + (string tweetNumber) + " : " + tweet)

                             let mutable followersBag : ConcurrentBag<string> = followersTable.Item(username)
                             let mutable followersArray : string array = followersBag.ToArray()
                             let mutable feedTweet : string = "Original Tweeter : " + username + " ,Tweet : " + tweet
                             for i in 0 .. (followersArray.Length - 1) do
                                    //feed table of all  followers
                                    let mutable feedBag : ConcurrentBag<string> = feedTable.Item(followersArray.[i])
                                    
                                    feedBag.Add(feedTweet)
                                    let mutable userPath : string = "akka.tcp://ClientActorSystem@0.0.0.0:8080/user/" + followersArray.[i]
                                    let mutable userRef = system.ActorSelection(userPath)
                                    //feed notification to all followers
                                    userRef <! (1,1,1,1,1,1,username, feedTweet)

                                //update mention table 
                             let mutable  (mentionList,hashtagList) = matchHashTagsAndMentions(tweet)
                             for i in 0 .. mentionList.Count - 1 do
                                    //printfn "%s" (mentionList.Item(i))
                                    if (mentionsTable.ContainsKey(mentionList.Item(i)) ) then
                                        let mutable mentionsBag : ConcurrentBag<string> = mentionsTable.Item(mentionList.Item(i))
                                        mentionsBag.Add(feedTweet)
                                    else
                                        let mutable mentionBagObj : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        mentionBagObj.Add(feedTweet)
                                        let mutable tryAddFlag2 : bool = false
                                        while (not tryAddFlag2) do
                                          tryAddFlag2 <-  mentionsTable.TryAdd(mentionList.Item(i),mentionBagObj)
                             for i in 0 .. hashtagList.Count - 1 do
                                    if (hashTagsTable.ContainsKey(hashtagList.Item(i)) ) then
                                        let mutable hashTagsBag : ConcurrentBag<string> = hashTagsTable.Item(hashtagList.Item(i))
                                        hashTagsBag.Add(feedTweet)
                                    else
                                        let mutable hashTagsBagObj : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        hashTagsBagObj.Add(feedTweet)
                                        let mutable tryAddFlag3 : bool = false
                                        while (not tryAddFlag3) do
                                          tryAddFlag3 <-  hashTagsTable.TryAdd(hashtagList.Item(i),hashTagsBagObj)
                                                            
     | :? Retweet as msg -> let mutable (int1,int2,int3,int4,int5, ActualTweeter , Retweeter , tweet) = unbox<Retweet> msg
                            let mutable followersBag : ConcurrentBag<string> = followersTable.Item(Retweeter)
                            let mutable followersArray : string array = followersBag.ToArray()
                            for i in 0 .. (followersArray.Length - 1) do
                                    //feed table of all  followers
                                    let mutable feedBag : ConcurrentBag<string> = feedTable.Item(followersArray.[i])
                                    feedBag.Add("Retweet from :  " + Retweeter + " ,tweet : " + tweet)
                                    let mutable userPath : string = "akka.tcp://ClientActorSystem@0.0.0.0:8080/user/" + followersArray.[i]
                                    let mutable userRef = system.ActorSelection(userPath)
                                    //feed notification to all followers
                                    userRef <! (1,1,1,1,1,1,Retweeter, "Retweet from :  " + Retweeter + " ,tweet : " + tweet) 
                            x.Sender <! (1,1.5,1.5,"Server Response : Retweet from :  " + Retweeter + " ,tweet : " + tweet)


     | :? HashTagQuery as msg -> let mutable (int1, int2, int3, hashtag) =  unbox<HashTagQuery> msg
                                 if (hashTagsTable.ContainsKey(hashtag)) then
                                    let mutable hashTagsBag : ConcurrentBag<string> = hashTagsTable.Item(hashtag)
                                    let mutable hashTaggedTweets : string array =  hashTagsBag.ToArray()
                                    let mutable allTweetsContainingHashtag : string = ""
                                    for i in 0 .. hashTaggedTweets.Length - 1 do
                                        allTweetsContainingHashtag <- allTweetsContainingHashtag + " , " + hashTaggedTweets.[i]
                                    x.Sender <! (true ,hashtag, allTweetsContainingHashtag)
                                 else
                                    x.Sender <! (false ,hashtag, "")


     | :? MentionQuery as msg -> let mutable (int1, int2, int3, int4, mention) =  unbox<MentionQuery> msg
                                 if (mentionsTable.ContainsKey(mention)) then
                                    let mutable mentionsBag : ConcurrentBag<string> = mentionsTable.Item(mention)
                                    let mutable mentionTweets : string array =  mentionsBag.ToArray()
                                    let mutable allTweetsContainingMentions : string = ""
                                    for i in 0 .. mentionTweets.Length - 1 do
                                        allTweetsContainingMentions <- allTweetsContainingMentions + " , " + mentionTweets.[i]
                                    x.Sender <! (true ,true, mention ,allTweetsContainingMentions)
                                 else
                                    x.Sender <! (false , false, mention ,"")      

     | :? GetAllUserTweets as msg -> let mutable (int1, int2, int3, int4, int5 , userId) =  unbox<GetAllUserTweets> msg 
                                     if (tweetsTable.ContainsKey(userId)) then
                                         let mutable tweetsBag : ConcurrentBag<string> = tweetsTable.Item(userId)
                                         let mutable tweetsArray : string array =  tweetsBag.ToArray()
                                         let mutable allTweetsFromUser: string = ""
                                         for i in 0 .. tweetsArray.Length - 1 do
                                            allTweetsFromUser <- allTweetsFromUser + " , " + tweetsArray.[i]
                                         x.Sender <! (true ,true, true, userId , allTweetsFromUser)
                                     else
                                         x.Sender <! (true ,true, true, userId , "")
                                                                          

     | _ -> failwith "wrong message format for child node"

for i in 1 .. TotalNumberOFServerInstances do
    let mutable serverId : string = "server" + string(i)
    let userActorRef = system.ActorOf(Props(typedefof<ServerActor>),serverId)
    printfn "server created with serverId %s" serverId

while not ExecutionShutDownFlag do
    ignore ()

