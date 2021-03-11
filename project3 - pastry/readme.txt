=================================DOS- PROJECT 3 - Pastry ========================================


Group Members: I have done this project alone, hence i dont have any group members.

Name  :  Vikas Chaubey
UFID  :  35115826
Email :  vikas.chaubey@ufl.edu


Note : Please use following command for program execution if following error occurs ( error FS3302: The package management feature requires language version 5.0 use /langversion:preview ))

      dotnet fsi --langversion:preview project3.fsx  10000 100





What is working?

With this project I have successfully implemented Pastry API. Each node in the pastry network is implemented to have a unique identifier.This identifier is generated using base 4 number system.When a message is sent to any pastry node then it routes the message to the node which is numerically closest to it.Each node in the cluster keep track of its closest neighbors.The implemented algorithm works seamlessly for the given inputs defined in the problem statement.





The biggest problems solved:


Input :

Total number of  nodes : 10000

Total number of requests per peer :  10

Command : dotnet fsi --langversion:preview project3.fsx  10000 10

Program Output:

routing table connected
total number of nodes : 10000
total number of requests : 10
average number of hops per Route: 2.30
Total Time taken for Execution : 27116L Milliseconds
Real: 00:00:27.441, CPU: 00:01:42.196, GC gen0: 2129, gen1: 243, gen2: 4


This is the biggest problem I managed to solve, I also tried inputs such as 20000 nodes with 100 peer requests but then program was taking long time to converge.


