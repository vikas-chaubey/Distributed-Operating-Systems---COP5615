=================================DOS- PROJECT 2 - failure Model ========================================


Group Members: I have done this project alone, hence i dont have any group members.

Name  :  Vikas Chaubey
UFID  :  35115826
Email :  vikas.chaubey@ufl.edu

This Section of project implements Permanent node failure model for gossip and push sum algorithms

Note : Please use following command for program execution if following error occurs ( error FS3302: The package management feature requires language version 5.0 use /langversion:preview ))

dotnet fsi --langversion:preview failuremodel.fsx totalNumberofNodes failureNodeNumber topology algorithm

- totalNumberofNodes = total number of nodes in cluster
- failureNodeNumber = number of nodes that should fail in cluster while execution of algos 
- topology = topology name (line, full, 2D, imp2D)
- algorithm = algorithm name (gossip or push-sum)


What is working?

Performed Experiment: The idea was to observe the effect of failed number of nodes on the convergence of gossip as well as push-sum algorithms for all topologies such as line, full, 2D and imperfect 2D.
These observations are made using following constraints:
1) For the observation of effect of node failures on convergence of topologies, The total number of nodes in cluster are kept as constant, also for that cluster size it is known in advance that the topology converges successfully if there are no node failures, the convergence time is also known in case of both the algorithms.
2) By keeping the cluster size constant and increasing the failure node input on each run, we can determine the total number of nodes which were converged and with that data we can analyze how node failures impact convergence of topologies in case of both algorithms.
3) Ideally on increasing the failure nodes the convergence of nodes should decrease
4) Since in case of failure, topology will never converge fully hence in order to save the execution flow from entering an infinite loop or In case program gets stuck at certain node because messages can’t be transmitted ahead, A timeout period is used, this timeout period is equal to the topology successful convergence time for that cluster size which we know in advance. Using this timer we are making sure that if the topology has not converged within the known convergence period , this means some kind of failure occurred, we will wait for some extra time than actual convergence time and determine the
number of nodes which were actually converged before blind spot occurred in the program execution.


The biggest problems solved:

1) In case of Gossip algorithm : all topologies were tested with 1000 total cluster nodes.The number of failed nodes was increased on each run (max. till 300) and number of converged nodes was recorded.
2) In case of Push- Sum Algorithm : all topologies were tested  with 200 nodes.The number of failed nodes was increased on each run(max. till 40) and number of converged nodes was recorded.


Please check the project report for detailed documentation of implementation and observations made for this project.project report has detailed explanation of project implementation and it also involves the graphs.