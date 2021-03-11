=================================DOS- PROJECT 2 - Gossip ========================================


Group Members: I have done this project alone, hence i dont have any group members.

Name  :  Vikas Chaubey
UFID  :  35115826
Email :  vikas.chaubey@ufl.edu


Note : Please use following command for program execution if following error occurs ( error FS3302: The package management feature requires language version 5.0 use /langversion:preview ))

      dotnet fsi --langversion:preview project2.fsx  100 line gossip



What is working?

In order to resolve following issues which were faced during implementation :
 
1) blind spot issue among child nodes where they were not receiving rumors or message in case of gossip algorithm (especially in case of line topology)
2) obtaining convergence fir s,w ratio among nodes with precision of [10 power (- 10) ] ,i.e. up-to ten decimal places

Few workarounds are implemented in the code, with those workarounds all the topologies are converging in case of both algorithms i.e.

1) line, full, 2D and imp2D topologies are converging in case of Gossip algorithm  and all nodes are able to receive the rumor at-least 10 times.
2) s,w ratios are converging in all nodes for line, full, 2d and imp2D topologies in case of Push - Sum algorithm with precision of 10 decimal places.



The biggest problems solved:

1) In case of Gossip algorithm : all topologies were tested and converged  successfully with 10000 nodes.
2) In case of Push- Sum Algorithm : all topologies were tested and converged successfully with 500 nodes.


Please check the project report for detailed documentation of implementation and observations made for this project.project report has detailed explanation of project implementation and it also involves the graphs.