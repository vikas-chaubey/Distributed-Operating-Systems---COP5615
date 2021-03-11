// ActorSayHello.fsx
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
// #load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit
open System.Numerics
open System.Collections.Generic

// Custom Square Root functionality for BigInteger data type , because f# sqrt function does not support bigIntegers and also its producing wrong results in some case , 
// Hence i decided to implement a custom squarre root function for bigintegers.
//*******************************************************************************************************************************
//constant value for  10^1
let tenConstantValue = 10I  
// constant value for 10^10 
let tenBillionConstantValue = 10000000000I 
// constant value for 10^100
let googolConstantValue = 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000I 
//// constant value for 10^10000
let googolToTenthConstantValue = googolConstantValue * googolConstantValue * googolConstantValue * googolConstantValue * googolConstantValue * googolConstantValue * googolConstantValue * googolConstantValue * googolConstantValue * googolConstantValue 

//this code block calculates the exponents of power 10  of any number
let private power10 (exponentValue : BigInteger) =
    if exponentValue.Sign = -1 then
        0I
    else
        let rec exponentFinder (productValue : BigInteger) (powwerValue : BigInteger) multiplierValue log10OfMultValue =
            match powwerValue with
            | x when x < log10OfMultValue -> productValue, powwerValue
            | _ -> exponentFinder (productValue * multiplierValue) (powwerValue - log10OfMultValue) multiplierValue log10OfMultValue
        let pow10To1000, rem1 = exponentFinder 1I exponentValue googolToTenthConstantValue 1000I
        let pow10To100, rem2 = exponentFinder 1I rem1 googolConstantValue 100I
        let pow10To10, rem3 = exponentFinder 1I rem2 tenBillionConstantValue 10I
        pow10To1000 * pow10To100 * pow10To10 * fst(exponentFinder 1I rem3 10I 1I)

//this code block calculates the log base 10 value of any number
let private log10 inputNumber =
    // check if number is negative then throw error
    if inputNumber <= 0I then
        failwith "Error :Logarithm of a non-positive number is not defined."
    //  count the number of times divisor will divide num
    let rec divPerformer countValue (numTemp : BigInteger) divisorValue =
        match numTemp with
        | x when x < divisorValue -> countValue, numTemp
        | _ -> divPerformer (countValue + 1I) (numTemp / divisorValue) divisorValue
    let thousandsCountValue, rem1Value = divPerformer 0I inputNumber googolToTenthConstantValue
    let hundredsCountValue, rem2Value = divPerformer 0I rem1Value googolConstantValue
    let tensCountValue, rem3Value = divPerformer 0I rem2Value tenBillionConstantValue
    1000I * thousandsCountValue + 100I * hundredsCountValue + tenConstantValue * tensCountValue + fst(divPerformer 0I rem3Value 10I)


let bigintSqrt(bigIntNumber : BigInteger) =
    //make rough guess of squarre root using log and power functions
    let sqrtRoughGuessValue (numberValue : BigInteger) =
        let log10x = log10 (numberValue + 1I)
        let halfLogValue = (log10x + 1I) >>> 1
        (power10 halfLogValue)
    //do converge calculations    
    let rec convergeCal previousGuess =
        let nextGuessValue = (bigIntNumber / previousGuess + previousGuess) >>> 1
        match BigInteger.Abs (previousGuess - nextGuessValue) with
        | x when x < 2I -> nextGuessValue
        | _ -> convergeCal nextGuessValue
    // if bigint sign is negative throw error
    if bigIntNumber.Sign = -1 then
        failwith "Error : Square root of a negative number is not defined."
    else
        //proceed with positive value
        let root = convergeCal (sqrtRoughGuessValue bigIntNumber)
        if root * root > bigIntNumber then
            root - 1I
        else
            root
//*******************************************************************************************************************************



//*******************************************************************************************************************************

// Actual program starts here
//fetch command line arguments given as input for execution
let args : string array = fsi.CommandLineArgs |>  Array.tail
// fetch the first argument - last number in sequence calculation
let LastNumber = System.Numerics.BigInteger.Parse(args.[0])
//fetch the second argument - the length of sequence
let SequenceLength = System.Numerics.BigInteger.Parse(args.[1])

type InputTriple = bigint * bigint * bigint
type StartEvaluationTuple = bigint * bigint
//create actor system object to spawn worker instances
let system = ActorSystem.Create("FSharp")

// this code Block defines a worker actor
type  WorkerActor =
  inherit Actor
  override x.OnReceive message =
    match message with
      | :? InputTriple as msg -> 
             let (startPoint,endPoint,sequenceLength) = unbox<InputTriple> message
             // this code block takes numbers in the given range one by one and form the sequence of desired length
             // then check if the squarred sum of all numbers in the sequence is a perfect square or not , if its squarred sum is perfect square the number is printed
             for currentStartPoint in startPoint .. endPoint do
                let mutable squarredSumValue = bigint 0
                let constantOne = bigint 1
                for tempValue in currentStartPoint .. (currentStartPoint + SequenceLength - constantOne) do
                   let productValue = tempValue * tempValue
                   squarredSumValue <- squarredSumValue + productValue
                let squareRootValue = bigintSqrt (squarredSumValue)
                if (squareRootValue * squareRootValue = squarredSumValue) then
                  printfn "%A \n" currentStartPoint

      | _ -> failwith "wrong input to worker"


 
// this code Block defines a Master actor which supervises all worker actors spawned
type SupervisorActor =
  inherit Actor
  override x.OnReceive message =
    match message with
    | :? StartEvaluationTuple as msg -> 
           let (endPoint,sequenceLength) = unbox<StartEvaluationTuple> message
           //these parameters are used to calculate task segment ranges for wroker actors
           let remainderValue : bigint = endPoint % (bigint 16)
           let actualEndPointValue : bigint = endPoint -  remainderValue
           let startPointValue : bigint = bigint 1
           
           //constants used in the processing of data
           let constantOne : bigint = bigint 1
           let zeroValue : bigint = bigint 0
           
           // with this parameter value we can define how many worker actors will be spawned for the processing of input, the value that i found optimus is 16 
           // current machine has 8 cores , increasing the value of workers more than 16 did not result in significant improvement of performance (cpu/real time ratio almost remains same on further increase)
           let numberOfWorkerActors = bigint 16
           

           if (endPoint>(numberOfWorkerActors-constantOne)) then
              let mutable startSegment = bigint 0
              let mutable secondSegment = bigint 0
              let mutable prevSecondSegment = bigint 0
              //calculation of task segment ranges , and creating the 
              for i in constantOne .. numberOfWorkerActors do
                 let index = i - (bigint 1)
                 if (index = zeroValue) then
                    startSegment <- startPointValue
                 else
                    startSegment <- prevSecondSegment + constantOne
                 
                 if (i = numberOfWorkerActors) then
                    secondSegment <- actualEndPointValue
                 else
                    secondSegment <- i * endPoint/numberOfWorkerActors
                 
                 prevSecondSegment <- secondSegment
                 
                 system.ActorOf(Props(typedefof<WorkerActor>,Array.empty)) <! (startSegment,secondSegment,sequenceLength)    
           else
               //do nothing

           // process remaining tasks by spawning a separate worker actor
           if ( remainderValue <> zeroValue || actualEndPointValue = zeroValue) then
              let worker17 = system.ActorOf(Props(typedefof<WorkerActor>,Array.empty))
              worker17 <! (actualEndPointValue+constantOne,endPoint,sequenceLength)

    | str1->  failwith "wrong input to worker"

                     
//create the instance for master actor and pass the input to master worker.              
let supervisorActor = system.ActorOf(Props(typedefof<SupervisorActor>,Array.empty))
supervisorActor <! (LastNumber,SequenceLength)
//Terminate the actor system once all actors have provided output
system.Terminate()
system.WhenTerminated.Wait()