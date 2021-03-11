// ActorSayHello.fsx
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
// #load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.FSharp
open System.Collections.Generic
    
type RespondToMaster = int
type BuildNodeCluster = string * bool
type StartClusterRouting = int * bool
type RouteToNode = string * int * string * IActorRef
type InitiateNode = string * int * Dictionary<string,IActorRef> * (string array) * string[,] * (string array) * (string array)

let mutable ExecutionShutDownFlag = false
let args : string array = fsi.CommandLineArgs |>  Array.tail//
//fetch the first argument - total number of nodes in the system
let totalNumberOfNodes = args.[0] |> int
//fetch the second argument - peer requests
let totalnumRequests = args.[1] |> int

let mutable WorkerActorrRefArray : Object array = Array.zeroCreate totalNumberOfNodes
   
let mutable SupervisorObj : IActorRef = Unchecked.defaultof<_>
printfn "total number of nodes : %i" totalNumberOfNodes
printfn "total number of requests : %i" totalnumRequests


let system = ActorSystem.Create("ActorSystemSupervisor")


type ChildActorNode (numberOfNodes : int) =
    inherit Actor()
    let mutable sourceActorId : string = ""
    let mutable numRequests : int  = 0
    let mutable nodesDictionary: Dictionary<string,IActorRef> = new Dictionary<string,IActorRef>()
    let mutable nodeIdList : string array = Array.zeroCreate numberOfNodes
    let mutable routingTable2DArray : string[,] = Array2D.zeroCreate 8 4
    let mutable leafNodeActorInstance  : string array = Array.zeroCreate 8
    let mutable neighborNodesArray : string array = Array.zeroCreate 8
    let mutable hopCountValue : int = 0
    let mutable I : int = 0
    override x.OnReceive message =
        match message with
        | :? RouteToNode as msg ->   let mutable (destinationNodeNodeId,hopsCompleted,pastryMessage,master) = unbox<RouteToNode> msg
                                     let mutable  forwardToNode : string = sourceActorId
                                     let mutable  unionSetArray : string array = Array.zeroCreate 48
                                     let mutable  indexValueOfunionSetArray : int = 0
                                     let mutable  nextActorNode : string =  ""
                                     let mutable  indexValue : int = 0
                                     let mutable  rowNumber : int = 0
                                     let mutable  columnNumber  : int = 0
                                     let mutable  loopCounterVar : int = 0
                                     let mutable  shortestDistanceValue  : int = 0
                                     let mutable  destinationNode : int =  (int (destinationNodeNodeId))
                                     let mutable  hopsCompleted : int = (hopsCompleted % 15)
                                     let mutable  prefixMatchCurrentnode : int = 0
                                     let mutable  unionSetList : List<string> = new List<string>()
                                     let mutable  destinationNodes : List<string> = new List<string>()
                                     let mutable  visitFlag : bool = false
                                     hopCountValue <- hopsCompleted
                                     if (destinationNodes.Contains(destinationNodeNodeId)) then
                                         visitFlag <- true
                                     else
                                         let mutable  destinationNodesTemp : List<string> = new List<string>()
                                         destinationNodesTemp.Add(destinationNodeNodeId)
                                         destinationNodesTemp.AddRange(destinationNodes)
                                         destinationNodes <- destinationNodesTemp
                                     if sourceActorId = destinationNodeNodeId then
                                         master <! (hopCountValue) //RespondToMaster
                                     if ((destinationNode >= (int (leafNodeActorInstance.[0]))) && (destinationNode <= (int (leafNodeActorInstance.[7])))) then
                                         shortestDistanceValue <- Math.Abs( destinationNode - (int (leafNodeActorInstance.[0])))
                                         forwardToNode <- leafNodeActorInstance.[0]
                                         
                                         for loopCounterVar in 1 .. 7 do
                                             if (Math.Abs(destinationNode - (int (leafNodeActorInstance.[loopCounterVar]))) < shortestDistanceValue) then
                                                 shortestDistanceValue <- Math.Abs(destinationNode - ( int (leafNodeActorInstance.[loopCounterVar]))) 
                                                 forwardToNode <- leafNodeActorInstance.[loopCounterVar]
                                     else
                                         let mutable whileLoopFlag : bool =  true
                                         while (loopCounterVar < 7) && whileLoopFlag do
                                             if (sourceActorId.[0..(loopCounterVar-1)] = destinationNodeNodeId.[0..(loopCounterVar-1)]) then
                                                prefixMatchCurrentnode <- loopCounterVar
                                                loopCounterVar <- loopCounterVar + 1
                                             else 
                                                 whileLoopFlag <- false
                                                 loopCounterVar <- loopCounterVar + 1
                                         rowNumber <- prefixMatchCurrentnode 
                                         columnNumber <- (int (string destinationNodeNodeId.[prefixMatchCurrentnode]))
                                         if ((not (isNull routingTable2DArray.[rowNumber , columnNumber])) && (prefixMatchCurrentnode > 0) ) then
                                             forwardToNode <- routingTable2DArray.[rowNumber , columnNumber]
                                         else
                                             let mutable indexValueTest : int = 0
                                             unionSetList.InsertRange(0 , neighborNodesArray)
                                             unionSetList.InsertRange(neighborNodesArray.Length, leafNodeActorInstance)
                                             indexValue <- 0
                                             loopCounterVar <- 0
                                             let mutable check : int = 0
                                             indexValueOfunionSetArray <- 16
                                             while (check < 32) do
                                                for loopCounterVar in 0 .. 3 do
                                                    if (not (isNull routingTable2DArray.[indexValue,loopCounterVar])) then
                                                        let mutable  unionSetListTemp : List<string> = new List<string>()
                                                        unionSetListTemp.Add(routingTable2DArray.[indexValue , loopCounterVar])
                                                        unionSetListTemp.AddRange(unionSetList)
                                                        unionSetList <- unionSetListTemp
                                                    check <- check + 1
                                                indexValue <- indexValue + 1
                                             
                                             let mutable prefixMatchdestinationNode : int = -1
                                             indexValue <- 0
                                             forwardToNode <- sourceActorId
                                             let mutable flag : bool = true
                                             let mutable i : int = 1
                                             while(indexValue < unionSetList.Count - 1) do
                                                if (not (isNull (unionSetList.Item(indexValue)))) then
                                                    nextActorNode <- unionSetList.Item(indexValue)
                                                    while ( (i < 8) && flag) do
                                                        if (nextActorNode.[0..i] = destinationNodeNodeId.[0..i]) then
                                                            prefixMatchdestinationNode <- i
                                                        else 
                                                            flag <- false
                                                        i<- i + 1

                                                    if (prefixMatchdestinationNode > prefixMatchCurrentnode) then
                                                        prefixMatchCurrentnode <-  prefixMatchdestinationNode
                                                        forwardToNode <- nextActorNode
                                                    else
                                                        if (prefixMatchdestinationNode = prefixMatchCurrentnode) then
                                                            if (Math.Abs((int (nextActorNode)) - destinationNode) < Math.Abs((int (sourceActorId)) - destinationNode)) then
                                                                prefixMatchCurrentnode <-  prefixMatchdestinationNode
                                                                forwardToNode <- nextActorNode
                                                    indexValue <- indexValue + 1
                                     
                                     if (forwardToNode = sourceActorId) then
                                          master <! (hopCountValue+I)
                                     else
                                          if not(visitFlag) then
                                               nodesDictionary.Item(forwardToNode) <! (destinationNodeNodeId,hopCountValue+1,pastryMessage,master)
                                          else 
                                               nodesDictionary.Item(forwardToNode) <! (destinationNodeNodeId,hopCountValue,pastryMessage,master)
               
        | :? InitiateNode as msg ->    let (nodeId1,numRequests1,nodesDictionary1,nodeIdList1,routingTable2DArray1,leafNodeActorInstance1,neighborNodesArray1) = unbox<InitiateNode> msg
                                       sourceActorId <- nodeId1
                                       numRequests <- numRequests1
                                       nodesDictionary <- nodesDictionary1
                                       nodeIdList <- nodeIdList1
                                       routingTable2DArray <- routingTable2DArray1
                                       leafNodeActorInstance <- leafNodeActorInstance1
                                       neighborNodesArray <- neighborNodesArray1
                                       I <- int( Math.Ceiling( float(((string totalNumberOfNodes).Length) /2)))
                                                          

//this code block defines a supervisor actor node which supervises all processes
type SupervisorActorNode(numberOfNodes: int , numRequests: int) =
   inherit Actor()
   let nodesDictionary = new Dictionary<string, IActorRef>()
   let mutable numLog : float = (Math.Log(float totalNumberOfNodes) / Math.Log(float 16))
   let nodeIdArrayList : string array= Array.zeroCreate numberOfNodes
   let mutable sortedNodeList = new List<string>()
   let mutable totalhopCountValue: float = 0.0
   let mutable aggregateValue: float = 0.0
   let mutable averagehopCountValue = 0.0
   override x.OnReceive message =
        match message with
        | :? BuildNodeCluster as msg ->
                let mutable leafNodeActorInstance : string array = Array.zeroCreate 8
                let mutable neighborNodes : string array = Array.zeroCreate 8
                let mutable routingTable2DArray : string[,] = Array2D.zeroCreate 8 4

                printfn "Pastry Cluster Started"

                let mutable nodeCount : int  = 0
                let mutable digitalindexValue : int = 0
                let mutable nodeId : string = ""
                let mutable isNodeAdded : int = 0

                for nodeCount in 0 .. numberOfNodes - 1 do
                    let nodeName : string = string nodeCount
                    let nodeReference = (system.ActorOf(Props(typedefof<ChildActorNode>,[|box numberOfNodes |]), nodeName))
                    isNodeAdded <- 0
                    while isNodeAdded = 0 do
                        nodeId <- ""
                        digitalindexValue <- 0
                        while digitalindexValue <> 8 do
                            nodeId <- nodeId + string (System.Random().Next(4))
                            digitalindexValue <- digitalindexValue + 1
                        if not(nodesDictionary.ContainsKey(nodeId)) then
                            nodesDictionary.Add(nodeId, nodeReference)
                            nodeIdArrayList.[nodeCount] <- nodeId
                            isNodeAdded <- 1
                        printfn "Node : %s " nodeId 
                let keyset = nodesDictionary.Keys
                let tmpList = new List<string>()
               
                Seq.iter (fun key -> tmpList.Add(key)) keyset
                sortedNodeList <- tmpList
                sortedNodeList.Sort()
                let count = 0
                nodeId <- ""
                nodeCount <- 0
                
                for nodeCount in 0 .. (numberOfNodes - 1)  do
                    leafNodeActorInstance <- Array.zeroCreate 8
                    neighborNodes <- Array.zeroCreate 8
                    routingTable2DArray <- Array2D.zeroCreate 8 4 
                    nodeId <- nodeIdArrayList.[nodeCount]
                    let mutable nodeindexValue = 0
                    for nodeindexValue in 0 .. 7 do
                        let temp = ((nodeindexValue + 1) % numberOfNodes)
                        neighborNodes.[nodeindexValue] <- nodeIdArrayList.[temp]
                    
                    nodeindexValue <- sortedNodeList.IndexOf(nodeId)
                    let mutable loopCounterVar = 0
                    let mutable currentNodeindexValue = 0

                    if ((nodeindexValue-4) < 0) then
                        currentNodeindexValue <- 0
                    else if (nodeindexValue + 4) > (numberOfNodes - 1) then
                            currentNodeindexValue <- numberOfNodes - 9
                         else
                             currentNodeindexValue <- nodeindexValue - 4
                    

                    for loopCounterVar in 0..7 do
                        if currentNodeindexValue = nodeindexValue then
                            currentNodeindexValue <- currentNodeindexValue + 1
                        leafNodeActorInstance.[loopCounterVar] <- sortedNodeList.Item(currentNodeindexValue)
                        currentNodeindexValue <- currentNodeindexValue + 1
                    
                    let mutable filledCount = 0                   
                    let mutable flag : bool = false
                    let mutable column = 0
                    let mutable digit: char = ' ' 
                    digitalindexValue <- 0
                    let mutable isCellFilled : bool[,] = Array2D.zeroCreate 8 4 

                    for pair in nodesDictionary do
                        digitalindexValue <- 0
                        flag <- false
                        column <- 0
                        if pair.Key <> nodeId then
                            while not(flag) do
                                if pair.Key.[digitalindexValue] = nodeId.[digitalindexValue] then
                                    digitalindexValue <- digitalindexValue + 1
                                else
                                    column <- (int ( string (pair.Key.[digitalindexValue]) ))
                                    if  (isCellFilled.[digitalindexValue, column]) then
                                        routingTable2DArray.[digitalindexValue, column] <- pair.Key
                                        isCellFilled.[digitalindexValue, column] <- true
                                        flag <- true
                                    else
                                        flag <- true
                    nodesDictionary.Item(nodeId) <! (nodeId, numberOfNodes, nodesDictionary, nodeIdArrayList, routingTable2DArray, leafNodeActorInstance, neighborNodes)
                printfn "routing table connected"
                x.Self <! (1,true)

            | :? StartClusterRouting as msg ->
                let mutable k = 0
                let mutable requestCounter = numRequests
                for i in 0..numberOfNodes - 1 do
                    k<-i
                    while requestCounter > 0 do
                        k <- ((k+1) % numberOfNodes)
                        nodesDictionary.Item(nodeIdArrayList.[i]) <! (nodeIdArrayList.[k] , 0 , "Implement Pastry", x.Self)
                        requestCounter <- requestCounter - 1
                    requestCounter <- numRequests

            | :? RespondToMaster as msg  ->
                let (hopCountValue) = unbox<int> msg
                totalhopCountValue <- totalhopCountValue + (float hopCountValue)
                aggregateValue <- aggregateValue + (float 1)
                if (aggregateValue >= (float (numRequests * numberOfNodes))) then
                    System.Threading.Thread.Sleep(1000)
                    printfn "total number of nodes : %i" totalNumberOfNodes
                    printfn "total number of requests : %i" totalnumRequests
                    printfn "average number of hops per Route: %.2f" (totalhopCountValue/aggregateValue)
                    ExecutionShutDownFlag <- true

  
SupervisorObj <- system.ActorOf(Props(typedefof<SupervisorActorNode>,[|box totalNumberOfNodes;  box totalnumRequests |]), "SupervisorNode")
let timer = Diagnostics.Stopwatch.StartNew()
SupervisorObj <! ("initiate" , true)
while not ExecutionShutDownFlag do
    ignore ()
timer.Stop()
printfn "Total Time taken for Execution : %A Milliseconds" timer.ElapsedMilliseconds

       


                













  

    


